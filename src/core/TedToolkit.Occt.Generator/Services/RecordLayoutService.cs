// -----------------------------------------------------------------------
// <copyright file="RecordLayoutService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using ClangSharp;

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Context.Domains;
using ModularPipelines.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Builds and runs a native C++ probe to compute OCCT record layout.
/// </summary>
/// <param name="generationOptions">The generation options.</param>
/// <param name="typeService">The type naming service.</param>
/// <param name="vcpkgService">The vcpkg environment service.</param>
public sealed class RecordLayoutService(
    IOptions<GenerationOptions> generationOptions,
    ITypeService typeService,
    IVcpkgService vcpkgService) : IRecordLayoutService
{
    private const string PROBE_FOLDER_NAME = "__layout_probe";

    private const string SOURCE_FOLDER_NAME = "src";

    private const string BUILD_FOLDER_NAME = "build";

    private const string PROBE_TARGET_NAME = "occt_layout_probe";

    private const string CMAKE_FILE_NAME = "CMakeLists.txt";

    private const string PROBE_FILE_NAME = "layout_probe.cpp";

    private readonly Dictionary<string, long> _sizes = new(StringComparer.Ordinal);

    private readonly Dictionary<FieldKey, long> _offsets = [];

    /// <inheritdoc/>
    public async Task PrepareAsync(IEnumerable<CXXRecordDecl> records, IShellContext shell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(shell);

        var recordList = records.Select(r => r.Definition ?? r)
            .DistinctBy(GetRecordKey)
            .OrderBy(GetRecordKey, StringComparer.Ordinal)
            .ToArray();

        if (recordList.Length == 0)
        {
            return;
        }

        var sourceDirectory = Path.Combine(generationOptions.Value.CppFolder.FullName, PROBE_FOLDER_NAME, SOURCE_FOLDER_NAME);
        var buildDirectory = Path.Combine(generationOptions.Value.CppFolder.FullName, PROBE_FOLDER_NAME, BUILD_FOLDER_NAME);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(buildDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, CMAKE_FILE_NAME),
            await GenerateCMakeAsync().ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, PROBE_FILE_NAME),
            GenerateProbe(recordList),
            cancellationToken).ConfigureAwait(false);

        await RunCommandAsync(
            shell,
            "cmake",
            [
                "-S",
                sourceDirectory,
                "-B",
                buildDirectory,
                ZString.Concat("-DCMAKE_TOOLCHAIN_FILE=", Path.Combine(vcpkgService.GetRoot(), "scripts", "buildsystems", "vcpkg.cmake")),
                ZString.Concat("-DVCPKG_TARGET_TRIPLET=", vcpkgService.GetTriplet()),
            ],
            sourceDirectory,
            cancellationToken).ConfigureAwait(false);

        var buildArguments = new List<string>()
        {
            "--build",
            buildDirectory,
        };

        if (OperatingSystem.IsWindows())
        {
            buildArguments.AddRange(["--config", "Release",]);
        }

        await RunCommandAsync(
            shell,
            "cmake",
            buildArguments,
            sourceDirectory,
            cancellationToken).ConfigureAwait(false);

        var probePath = GetProbePath(buildDirectory);
        var output = await RunCommandAsync(
            shell,
            probePath,
            [],
            Path.GetDirectoryName(probePath) ?? buildDirectory,
            cancellationToken).ConfigureAwait(false);

        ParseProbeOutput(output);
    }

    /// <inheritdoc/>
    public long GetSize(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var key = GetRecordKey(record.Definition ?? record);
        if (_sizes.TryGetValue(key, out var size))
        {
            return size;
        }

        throw new InvalidOperationException($"Native layout probe did not produce sizeof record ({key}).");
    }

    /// <inheritdoc/>
    public long GetOffset(CXXRecordDecl record, FieldDecl field)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(field);

        var key = new FieldKey(GetRecordKey(record.Definition ?? record), field.Name);
        if (_offsets.TryGetValue(key, out var offset))
        {
            return offset;
        }

        throw new InvalidOperationException(
            $"Native layout probe did not produce offset for field ({key.RecordName}::{key.FieldName}).");
    }

    private static string EscapeCppString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string GetProbePath(string buildDirectory)
    {
        var executableName = OperatingSystem.IsWindows()
            ? ZString.Concat(PROBE_TARGET_NAME, ".exe")
            : PROBE_TARGET_NAME;

        foreach (var candidate in new[]
                 {
                     Path.Combine(buildDirectory, "Release", executableName),
                     Path.Combine(buildDirectory, executableName),
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Cannot find native layout probe executable under {buildDirectory}.");
    }

    private static async Task<string> RunCommandAsync(
        IShellContext shell,
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);

        var result = await shell.Command.ExecuteCommandLineTool(
                new GenericCommandLineToolOptions(fileName) { Arguments = arguments, },
                new CommandExecutionOptions() { WorkingDirectory = workingDirectory, ThrowOnNonZeroExitCode = false, },
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return result.StandardOutput;
        }

        throw new InvalidOperationException(
            $"""
            Command failed ({result.ExitCode}): {fileName} {string.Join(" ", arguments)}
            stdout:
            {result.StandardOutput}
            stderr:
            {result.StandardError}
            """);
    }

    private string GenerateProbe(CXXRecordDecl[] records)
    {
        var builder = ZString.CreateStringBuilder();
        builder.AppendLine("#include <cstddef>");
        builder.AppendLine("#include <iostream>");
        builder.AppendLine();
        builder.AppendLine("#define private public");
        builder.AppendLine("#define protected public");

        foreach (var fileName in generationOptions.Value.DeclOptions
                     .Select(o => o.FileName)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            builder.Append("#include <");
            builder.Append(fileName);
            builder.AppendLine(".hxx>");
        }

        builder.AppendLine("#undef protected");
        builder.AppendLine("#undef private");
        builder.AppendLine();

        for (var i = 0; i < records.Length; i++)
        {
            builder.Append("using record_");
            builder.Append(i.ToString(CultureInfo.InvariantCulture));
            builder.Append(" = ");
            builder.Append(GetRecordKey(records[i]));
            builder.AppendLine(";");
        }

        builder.AppendLine();
        builder.AppendLine("int main()");
        builder.AppendLine("{");

        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            var recordKey = GetRecordKey(record);
            builder.Append("    std::cout << \"S\\t");
            builder.Append(EscapeCppString(recordKey));
            builder.Append("\\t\" << sizeof(record_");
            builder.Append(i.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(") << '\\n';");

            foreach (var field in GetFields(record))
            {
                builder.Append("    std::cout << \"F\\t");
                builder.Append(EscapeCppString(recordKey));
                builder.Append("\\t");
                builder.Append(EscapeCppString(field.Name));
                builder.Append("\\t\" << offsetof(record_");
                builder.Append(i.ToString(CultureInfo.InvariantCulture));
                builder.Append(", ");
                builder.Append(field.Name);
                builder.AppendLine(") << '\\n';");
            }
        }

        builder.AppendLine("    return 0;");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private async Task<string> GenerateCMakeAsync()
    {
        var cppStandard = await vcpkgService.GetOcctCppVersionAsync().ConfigureAwait(false);
        return $$"""
            cmake_minimum_required(VERSION 3.28)
            project(TedToolkit_Occt_LayoutProbe LANGUAGES CXX)

            set(CMAKE_CXX_STANDARD {{cppStandard}})
            set(CMAKE_CXX_STANDARD_REQUIRED ON)
            set(CMAKE_CXX_EXTENSIONS OFF)

            find_package(OpenCASCADE CONFIG REQUIRED)

            add_executable({{PROBE_TARGET_NAME}} {{PROBE_FILE_NAME}})
            target_include_directories({{PROBE_TARGET_NAME}} PRIVATE ${OpenCASCADE_INCLUDE_DIR})
            target_link_libraries({{PROBE_TARGET_NAME}} PRIVATE ${OpenCASCADE_LIBRARIES})
            """;
    }

    private string GetRecordKey(CXXRecordDecl record)
    {
        return typeService.GetCppName(record.TypeForDecl);
    }

    private static IEnumerable<FieldDecl> GetFields(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var fieldDecl in GetFields(baseDecl.Definition ?? baseDecl))
            {
                yield return fieldDecl;
            }
        }

        foreach (var recordField in record.Fields)
        {
            yield return recordField;
        }
    }

    private void ParseProbeOutput(string output)
    {
        _sizes.Clear();
        _offsets.Clear();

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts is ["S", _, _])
            {
                _sizes[parts[1]] = long.Parse(parts[2], CultureInfo.InvariantCulture);
                continue;
            }

            if (parts is ["F", _, _, _])
            {
                _offsets[new FieldKey(parts[1], parts[2])] = long.Parse(parts[3], CultureInfo.InvariantCulture);
                continue;
            }

            throw new InvalidOperationException($"Unexpected native layout probe output line: {line}");
        }
    }

    private readonly record struct FieldKey(string RecordName, string FieldName);
}
