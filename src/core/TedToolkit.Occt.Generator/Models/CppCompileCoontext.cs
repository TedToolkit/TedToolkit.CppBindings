// -----------------------------------------------------------------------
// <copyright file="CppCompileCoontext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using ModularPipelines.Context.Domains;
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

    private readonly List<string> _fileNames = [];

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
        _fileNames.Add(name);
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
    /// <returns>The output directory containing the compiled binary.</returns>
    public async Task<DirectoryInfo> BuildAsync(
        IShellContext shell,
        bool isExecutable,
        string vcpkgRoot,
        string triplet,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_sourceDirectory.FullName, "CMakeLists.txt");
        await File.WriteAllTextAsync(path, GenerateCMake(isExecutable), cancellationToken).ConfigureAwait(false);

        await RunCommandAsync(
            shell,
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

        await RunCommandAsync(
            shell,
            "cmake",
            buildArguments,
            cancellationToken).ConfigureAwait(false);

        return _binaryDirectory;
    }

    /// <summary>
    /// Generates the transient <c>CMakeLists.txt</c> content.
    /// </summary>
    /// <param name="isExecutable">A value indicating whether to build an executable.</param>
    /// <returns>The generated CMake file content.</returns>
    private string GenerateCMake(bool isExecutable)
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
                 """;

        string GetAddingFiles(bool isExecutable)
        {
            return isExecutable
                ? $"add_executable({_projectName} {string.Join(" ", _fileNames)})"
                : $"add_library({_projectName} SHARED {string.Join(" ", _fileNames)})";
        }
    }

    private static async Task RunCommandAsync(
        IShellContext shell,
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);

        _ = await shell.Command.ExecuteCommandLineTool(
                new GenericCommandLineToolOptions(fileName) { Arguments = arguments, },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}