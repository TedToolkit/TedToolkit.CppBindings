// -----------------------------------------------------------------------
// <copyright file="RecordLayoutModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Context.Domains;
using ModularPipelines.Modules;
using ModularPipelines.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Prepares native C++ record layout data before generation starts.
/// </summary>
/// <param name="recordManager">The record queue manager.</param>
[DependsOn<ParseModule>]
public sealed class RecordLayoutModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordModelManager recordManager,
    IVcpkgService vcpkgService) : Module<bool>
{
    private const string PROBE_FOLDER_NAME = "__layout_probe";

    private const string SOURCE_FOLDER_NAME = "src";

    private const string BUILD_FOLDER_NAME = "build";

    private const string PROBE_TARGET_NAME = "occt_layout_probe";

    private const string CMAKE_FILE_NAME = "CMakeLists.txt";

    private const string PROBE_FILE_NAME = "layout_probe.cpp";

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sourceDirectory = Path.Combine(generationOptions.Value.CppFolder.FullName, PROBE_FOLDER_NAME,
            SOURCE_FOLDER_NAME);
        var buildDirectory =
            Path.Combine(generationOptions.Value.CppFolder.FullName, PROBE_FOLDER_NAME, BUILD_FOLDER_NAME);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(buildDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, CMAKE_FILE_NAME),
            await GenerateCMakeAsync().ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, PROBE_FILE_NAME),
            GenerateProbe(),
            cancellationToken).ConfigureAwait(false);

        await RunCommandAsync(
            context.Shell,
            "cmake",
            [
                "-S",
                sourceDirectory,
                "-B",
                buildDirectory,
                "-DCMAKE_BUILD_TYPE=Release",
                ZString.Concat("-DCMAKE_TOOLCHAIN_FILE=",
                    Path.Combine(vcpkgService.GetRoot(), "scripts", "buildsystems", "vcpkg.cmake")),
                ZString.Concat("-DVCPKG_TARGET_TRIPLET=", vcpkgService.GetTriplet()),
            ],
            sourceDirectory,
            cancellationToken).ConfigureAwait(false);

        var buildArguments = new List<string>()
        {
            "--build", buildDirectory, "--config", "Release",
        };

        await RunCommandAsync(
            context.Shell,
            "cmake",
            buildArguments,
            sourceDirectory,
            cancellationToken).ConfigureAwait(false);

        var probePath = GetProbePath(buildDirectory);
        var output = await RunCommandAsync(
            context.Shell,
            probePath,
            [],
            Path.GetDirectoryName(probePath) ?? buildDirectory,
            cancellationToken).ConfigureAwait(false);

        ParseProbeOutput(output);
        return true;
    }

    private void ParseProbeOutput(string output)
    {
        var valueArray = output.Split('\n');
        var queue = new Queue<long>(valueArray[..^1].Select(long.Parse));
        foreach (var record in recordManager.RecordModels)
        {
            record.Size = queue.Dequeue();

            foreach (var field in record.FieldModels)
            {
                field.Offset = queue.Dequeue();
            }
        }
    }

    private string GetProbePath(string buildDirectory)
    {
        var executableName = OperatingSystem.IsWindows()
            ? ZString.Concat(PROBE_TARGET_NAME, ".exe")
            : PROBE_TARGET_NAME;

        var probePath = Path.Combine(buildDirectory, executableName);
        if (File.Exists(probePath))
        {
            return probePath;
        }

        throw new FileNotFoundException($"Cannot find native layout probe executable under {buildDirectory}.");
    }

    private async Task<string> RunCommandAsync(
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

    private string GenerateProbe()
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
        builder.AppendLine("int main()");
        builder.AppendLine("{");

        foreach (var record in recordManager.RecordModels)
        {
            builder.Append("    std::cout << sizeof(");
            builder.Append(record.Type.SourceType);
            builder.AppendLine(") << '\\n';");

            foreach (var field in record.FieldModels)
            {
                builder.Append("    std::cout << offsetof(");
                builder.Append(record.Type.SourceType);
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
                 set_target_properties({{PROBE_TARGET_NAME}} PROPERTIES
                     RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
                     RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}")
                 target_include_directories({{PROBE_TARGET_NAME}} PRIVATE ${OpenCASCADE_INCLUDE_DIR})
                 target_link_libraries({{PROBE_TARGET_NAME}} PRIVATE ${OpenCASCADE_LIBRARIES})
                 """;
    }
}