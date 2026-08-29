// -----------------------------------------------------------------------
// <copyright file="CompilerProbeModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Compiles and runs native type traits, then completes record ownership facts.
/// </summary>
[DependsOn<ParseModule>]
public sealed class CompilerProbeModule : Module<bool>
{
    private readonly IRecordModelManager _recordManager;

    private readonly IOptions<GenerationOptions> _options;

    private readonly IVcpkgDefaultTripletResolver _defaultsResolver;

    private readonly IVcpkgEnvironment _vcpkgEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompilerProbeModule"/> class.
    /// </summary>
    /// <param name="recordManager">The parsed record source.</param>
    /// <param name="options">The generation options.</param>
    /// <param name="defaultsResolver">The default triplet resolver.</param>
    /// <param name="vcpkgEnvironment">The vcpkg environment.</param>
    internal CompilerProbeModule(
        IRecordModelManager recordManager,
        IOptions<GenerationOptions> options,
        IVcpkgDefaultTripletResolver defaultsResolver,
        IVcpkgEnvironment vcpkgEnvironment)
    {
        _recordManager = recordManager;
        _options = options;
        _defaultsResolver = defaultsResolver;
        _vcpkgEnvironment = vcpkgEnvironment;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var records = _recordManager.RecordModels
            .OrderBy(static record => record.Type.CppTypeName, StringComparer.Ordinal)
            .ToArray();
        if (records.Length is 0)
        {
            return true;
        }

        var temporaryDirectory = Directory.CreateTempSubdirectory("TedToolkit.Occt.Probe.");
        try
        {
            var sourcePath = Path.Combine(temporaryDirectory.FullName, "probe.cpp");
            var executablePath = Path.Combine(temporaryDirectory.FullName, "probe.exe");
            await File.WriteAllTextAsync(sourcePath, CreateSource(records), cancellationToken)
                .ConfigureAwait(false);
            await CompileAsync(sourcePath, executablePath, cancellationToken).ConfigureAwait(false);
            CompleteRecords(records, await RunAsync(executablePath, cancellationToken).ConfigureAwait(false));
            return true;
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    private static string CreateSource(RecordModel[] records)
    {
        var builder = new StringBuilder(
            "#include <cstddef>\n#include <iostream>\n#include <type_traits>\n"
            + "#include <Standard_Handle.hxx>\n#include <Standard_Transient.hxx>\n"
            + "static_assert(sizeof(opencascade::handle<Standard_Transient>) == sizeof(Standard_Transient*));\n"
            + "static_assert(alignof(opencascade::handle<Standard_Transient>) == alignof(Standard_Transient*));\n");
        foreach (var header in records.Select(static record => record.SourceHeader).Distinct(StringComparer.Ordinal))
        {
            _ = builder.Append("#include <").Append(header).Append(">\n");
        }

        _ = builder.Append("\nint main()\n{\n");
        for (var index = 0; index < records.Length; index++)
        {
            var type = records[index].Type.CppTypeName;
            _ = builder.Append("    std::cout << ").Append(index).Append(" << '\\t' << sizeof(")
                .Append(type).Append(") << '\\t' << alignof(").Append(type)
                .Append(") << '\\t' << std::is_trivially_copyable_v<").Append(type)
                .Append("> << '\\t' << std::is_trivially_destructible_v<").Append(type)
                .Append("> << '\\t' << std::is_destructible_v<").Append(type)
                .Append("> << '\\n';\n");
        }

        return builder.Append("    return 0;\n}\n").ToString();
    }

    private async Task CompileAsync(
        string sourcePath,
        string executablePath,
        CancellationToken cancellationToken)
    {
        var triplet = _options.Value.GetTriplet(_defaultsResolver);
        var startInfo = CreateStartInfo("clang++");
        startInfo.ArgumentList.Add($"-std=c++{_options.Value.CppVersion}");
        startInfo.ArgumentList.Add("-I" + _vcpkgEnvironment.GetOcctIncludeFolder(triplet));
        startInfo.ArgumentList.Add("-I" + _vcpkgEnvironment.GetIncludeFolder(triplet));
        foreach (var argument in _options.Value.CommandLineArgs)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(sourcePath);
        var libraryFolder = Path.Combine(
            _vcpkgEnvironment.GetRoot(),
            "installed",
            triplet,
            "lib");
        foreach (var library in Directory.EnumerateFiles(libraryFolder, "TK*.lib")
                     .Order(StringComparer.Ordinal))
        {
            startInfo.ArgumentList.Add(library);
        }

        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(executablePath);
        _ = await RunProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private Task<string> RunAsync(
        string executablePath,
        in CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(executablePath);
        var triplet = _options.Value.GetTriplet(_defaultsResolver);
        var nativeDirectory = Path.Combine(
            _vcpkgEnvironment.GetRoot(),
            "installed",
            triplet,
            "bin");
        startInfo.Environment["PATH"] = nativeDirectory
                                        + Path.PathSeparator
                                        + Environment.GetEnvironmentVariable("PATH");
        return RunProcessAsync(startInfo, cancellationToken);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName)
    {
        return new(fileName)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
    }

    private static async Task<string> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Could not start '{startInfo.FileName}'.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode is not 0)
        {
            throw new InvalidOperationException(
                $"Native compiler probe '{startInfo.FileName}' failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    /// <summary>
    /// Validates probe rows and completes the corresponding records.
    /// </summary>
    /// <param name="records">The records in probe order.</param>
    /// <param name="output">The probe process output.</param>
    /// <exception cref="InvalidOperationException">
    /// The output is incomplete, malformed, duplicated, or disagrees with parsed size evidence.
    /// </exception>
    internal static void CompleteRecords(RecordModel[] records, string output)
    {
        var lines = output.Split(['\r', '\n',], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != records.Length)
        {
            throw new InvalidOperationException(
                $"Native compiler probe returned {lines.Length} records; expected {records.Length}.");
        }

        var observed = new bool[records.Length];
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length is not 6
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || index < 0
                || index >= records.Length
                || observed[index]
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size)
                || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var alignment)
                || !TryParseBoolean(parts[3], out var triviallyCopyable)
                || !TryParseBoolean(parts[4], out var triviallyDestructible)
                || !TryParseBoolean(parts[5], out var isDestructible))
            {
                throw new InvalidOperationException($"Invalid native compiler probe row: '{line}'.");
            }

            var record = records[index];
            if (size != record.Size || alignment < 1)
            {
                throw new InvalidOperationException(
                    $"Native compiler probe disagrees with parsed layout for '{record.Type.CppTypeName}': "
                    + $"size {size}/{record.Size}, alignment {alignment}.");
            }

            record.Alignment = alignment;
            record.ObjectKind = GetObjectKind(record, triviallyCopyable, triviallyDestructible);
            if (record.ObjectKind is NativeObjectKind.Value)
            {
                record.MethodModels = record.MethodModels
                    .Where(static method => method.Type is not MethodModelType.DELETE)
                    .ToArray();
                NativeExportNameBuilder.Assign(record);
            }
            else if (record.ObjectKind is NativeObjectKind.Owned
                     && isDestructible
                     && !record.MethodModels.Any(static method => method.Type is MethodModelType.DELETE))
            {
                record.MethodModels =
                [
                    .. record.MethodModels,
                    CreateImplicitDestructor(),
                ];
                NativeExportNameBuilder.Assign(record);
            }
            else if (record.ObjectKind is NativeObjectKind.Handle
                     && isDestructible
                     && !record.MethodModels.Any(static method =>
                         method.Type is MethodModelType.VALUE_DELETE))
            {
                record.MethodModels =
                [
                    .. record.MethodModels,
                    CreateImplicitDestructor(MethodModelType.VALUE_DELETE),
                ];
                NativeExportNameBuilder.Assign(record);
            }

            observed[index] = true;
        }

        AddHandleReleaseOperations(records);
    }

    private static void AddHandleReleaseOperations(RecordModel[] records)
    {
        var handleTargets = records
            .SelectMany(static record => record.MethodModels)
            .SelectMany(static method => method.Parameters.Select(static parameter => parameter.Type)
                .Prepend(method.ReturnType))
            .Where(static type => type.IsOcctHandle)
            .Select(static type => type.OcctHandleElementCppType)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var record in records.Where(record =>
                     handleTargets.Contains(record.Type.CppTypeName)
                     && !record.MethodModels.Any(static method =>
                         method.Type is MethodModelType.HANDLE_RELEASE
                             or MethodModelType.DELETE
                             && method.NativeExportName.EndsWith("_Release", StringComparison.Ordinal))))
        {
            record.MethodModels =
            [
                .. record.MethodModels,
                CreateImplicitDestructor(MethodModelType.HANDLE_RELEASE),
            ];
            NativeExportNameBuilder.Assign(record);
        }
    }

    private static MethodModel CreateImplicitDestructor(
        MethodModelType methodType = MethodModelType.DELETE)
    {
        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = false,
            IsStatic = false,
            ReturnType = new()
            {
                CppTypeName = "void",
                CppValueTypeName = "void",
                CSharpPInvokeType = DataType.Void,
                CSharpPublicType = DataType.Void,
            },
            MethodName = "Delete",
            Type = methodType,
            Parameters = [],
        };
    }

    private static NativeObjectKind GetObjectKind(
        RecordModel record,
        bool triviallyCopyable,
        bool triviallyDestructible)
    {
        if (record.IsStandardTransient)
        {
            return NativeObjectKind.Handle;
        }

        return triviallyCopyable && triviallyDestructible
            ? NativeObjectKind.Value
            : NativeObjectKind.Owned;
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        if (value is "0")
        {
            result = false;
            return true;
        }

        if (value is "1")
        {
            result = true;
            return true;
        }

        result = false;
        return false;
    }
}