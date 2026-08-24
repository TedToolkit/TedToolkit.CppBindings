// -----------------------------------------------------------------------
// <copyright file="CppCompileCoontext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using ModularPipelines.Context.Domains;
using ModularPipelines.Exceptions;
using ModularPipelines.Options;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Tracks the transient C++ project used to compile generated native sources.
/// </summary>
internal sealed class CppCompileCoontext
{
    private readonly string _projectName;

    private readonly int _cppStandard;

    private readonly DirectoryInfo _sourceDirectory;

    private readonly DirectoryInfo _buildDirectory;

    private readonly DirectoryInfo _binaryDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CppCompileCoontext"/> class.
    /// </summary>
    /// <param name="cppFolder">The root C++ output folder.</param>
    /// <param name="projectName">The temporary project name.</param>
    /// <param name="cppStandard">The C++ language standard version.</param>
    public CppCompileCoontext(DirectoryInfo cppFolder, string projectName, int cppStandard)
    {
        _projectName = projectName;
        _cppStandard = cppStandard;
        ArgumentNullException.ThrowIfNull(cppFolder);
        var projectFolder = cppFolder.CreateSubdirectory(projectName);
        _sourceDirectory = projectFolder.CreateSubdirectory("src");
        _buildDirectory = projectFolder.CreateSubdirectory("build");
        _binaryDirectory = projectFolder.CreateSubdirectory("bin");
    }

    /// <summary>
    /// Adds one generated source file to the transient C++ project.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="source">The file content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the source file is written.</returns>
    public Task AddSourceAsync(string name, string source, in CancellationToken cancellationToken)
    {
        var path = Path.Combine(_sourceDirectory.FullName, name);
        return File.WriteAllTextAsync(path, source, cancellationToken);
    }

    /// <summary>
    /// Configures and builds the transient C++ project.
    /// </summary>
    /// <param name="shell">The shell context used to execute build tools.</param>
    /// <param name="isExecutable">A value indicating whether to build an executable instead of a shared library.</param>
    /// <param name="vcpkgRoot">The vcpkg root folder.</param>
    /// <param name="triplet">The target vcpkg triplet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compiled native artifact.</returns>
    /// <exception cref="InvalidOperationException">The command fails or the expected artifact is not produced.</exception>
    /// <exception cref="OperationCanceledException">The build is cancelled.</exception>
    public Task<FileInfo> BuildAsync(
        IShellContext shell,
        bool isExecutable,
        string vcpkgRoot,
        string triplet,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);

        return BuildAsync(RunCommandAsync, isExecutable, vcpkgRoot, triplet, cancellationToken);

        async Task<CppCommandResult> RunCommandAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken token)
        {
            try
            {
                var result = await shell.Command.ExecuteCommandLineTool(
                        new GenericCommandLineToolOptions(fileName) { Arguments = arguments, },
                        new CommandExecutionOptions() { ThrowOnNonZeroExitCode = false, },
                        cancellationToken: token)
                    .ConfigureAwait(false);
                return new(result.ExitCode, result.StandardOutput, result.StandardError);
            }
            catch (CommandException exception) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Native build command was cancelled.", exception, token);
            }
        }
    }

    /// <summary>
    /// Configures and builds the transient C++ project through a controlled command boundary.
    /// </summary>
    /// <param name="commandRunner">The native command runner.</param>
    /// <param name="isExecutable">A value indicating whether to build an executable instead of a shared library.</param>
    /// <param name="vcpkgRoot">The vcpkg root folder.</param>
    /// <param name="triplet">The target vcpkg triplet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compiled native artifact.</returns>
    /// <exception cref="InvalidOperationException">The command fails or the expected artifact is not produced.</exception>
    /// <exception cref="OperationCanceledException">The build is cancelled.</exception>
    internal async Task<FileInfo> BuildAsync(
        CppCommandRunner commandRunner,
        bool isExecutable,
        string vcpkgRoot,
        string triplet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandRunner);
        _ = await MaterializeProjectAsync(isExecutable, cancellationToken).ConfigureAwait(false);

        var artifact = GetExpectedArtifact(isExecutable);
        if (artifact.Exists)
        {
            artifact.Delete();
        }

        await RunCommandStageAsync(
            commandRunner,
            "configure",
            "cmake",
            [
                "-S",
                _sourceDirectory.FullName,
                "-B",
                _buildDirectory.FullName,
                "-DCMAKE_BUILD_TYPE=Release",
                ZString.Concat("-DCMAKE_TOOLCHAIN_FILE=",
                    Path.Combine(vcpkgRoot, "scripts", "buildsystems", "vcpkg.cmake")),
                ZString.Concat("-DVCPKG_TARGET_TRIPLET=", triplet),
                ZString.Concat("-DCMAKE_RUNTIME_OUTPUT_DIRECTORY_RELEASE=", _binaryDirectory.FullName),
                ZString.Concat("-DCMAKE_LIBRARY_OUTPUT_DIRECTORY_RELEASE=", _binaryDirectory.FullName),
            ],
            cancellationToken).ConfigureAwait(false);

        var buildArguments = new List<string>()
        {
            "--build", _buildDirectory.FullName, "--config", "Release",
        };

        await RunCommandStageAsync(
            commandRunner,
            "build",
            "cmake",
            buildArguments,
            cancellationToken).ConfigureAwait(false);

        artifact.Refresh();
        if (!artifact.Exists)
        {
            throw new InvalidOperationException(
                $"CMake build reported success but the expected native artifact was not produced: {artifact.FullName}");
        }

        return artifact;
    }

    /// <summary>
    /// Writes the deterministic transient <c>CMakeLists.txt</c> file.
    /// </summary>
    /// <param name="isExecutable">A value indicating whether to build an executable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized CMake project file.</returns>
    internal async Task<FileInfo> MaterializeProjectAsync(bool isExecutable, CancellationToken cancellationToken)
    {
        var compilationSources = _sourceDirectory
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(static file => IsCompilationSource(file.Extension))
            .Select(static file => file.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var path = Path.Combine(_sourceDirectory.FullName, "CMakeLists.txt");
        await File.WriteAllTextAsync(path, GenerateCMake(isExecutable, compilationSources), cancellationToken)
            .ConfigureAwait(false);
        return new(path);
    }

    /// <summary>
    /// Generates the transient <c>CMakeLists.txt</c> content.
    /// </summary>
    /// <param name="isExecutable">A value indicating whether to build an executable.</param>
    /// <param name="fileNames">The deterministic compilation-source names.</param>
    /// <returns>The generated CMake file content.</returns>
    private string GenerateCMake(bool isExecutable, IReadOnlyList<string> fileNames)
    {
        return $$"""
                 cmake_minimum_required(VERSION 3.28)

                 project({{_projectName}} LANGUAGES CXX)

                 set(CMAKE_CXX_STANDARD {{_cppStandard}})
                 set(CMAKE_CXX_STANDARD_REQUIRED ON)
                 set(CMAKE_CXX_EXTENSIONS OFF)

                 find_package(OpenCASCADE CONFIG REQUIRED)

                 {{GetAddingFiles(isExecutable)}}

                 target_include_directories({{_projectName}} PRIVATE ${OpenCASCADE_INCLUDE_DIR})
                 target_link_libraries({{_projectName}} PRIVATE ${OpenCASCADE_LIBRARIES})

                 if(MSVC OR CMAKE_CXX_SIMULATE_ID STREQUAL "MSVC")
                     target_compile_options({{_projectName}} PRIVATE /EHsc)
                 endif()
                 """;

        string GetAddingFiles(bool isExecutable)
        {
            return isExecutable
                ? $"add_executable({_projectName} {string.Join(" ", fileNames)})"
                : $"add_library({_projectName} SHARED {string.Join(" ", fileNames)})";
        }
    }

    private FileInfo GetExpectedArtifact(bool isExecutable)
    {
        string fileName;
        if (isExecutable)
        {
            if (OperatingSystem.IsWindows())
            {
                fileName = $"{_projectName}.exe";
            }
            else
            {
                fileName = _projectName;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            fileName = $"{_projectName}.dll";
        }
        else if (OperatingSystem.IsMacOS())
        {
            fileName = $"lib{_projectName}.dylib";
        }
        else
        {
            fileName = $"lib{_projectName}.so";
        }

        return new(Path.Combine(_binaryDirectory.FullName, fileName));
    }

    private static bool IsCompilationSource(string extension)
    {
        return extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cxx", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cc", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RunCommandStageAsync(
        CppCommandRunner commandRunner,
        string stage,
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        CppCommandResult result;
        try
        {
            result = await commandRunner(fileName, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{fileName} {stage} failed to execute.", exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (result.ExitCode == 0)
        {
            return;
        }

        var diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        throw new InvalidOperationException(
            $"{fileName} {stage} failed with exit code {result.ExitCode}: {diagnostic.Trim()}");
    }
}
