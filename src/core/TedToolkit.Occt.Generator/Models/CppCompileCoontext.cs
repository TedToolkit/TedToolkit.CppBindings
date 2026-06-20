using Cysharp.Text;

using ModularPipelines.Context.Domains;
using ModularPipelines.Options;

namespace TedToolkit.Occt.Generator.Models;

public sealed class CppCompileCoontext
{
    private readonly string _projectName;
    private readonly int _cppStandard;

    private readonly DirectoryInfo _sourceDirectory;
    private readonly DirectoryInfo _buildDirectory;
    private readonly DirectoryInfo _binaryDirectory;

    private readonly List<string> _fileNames = [];

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

    public Task AddSourceAsync(string name, string source, CancellationToken cancellationToken)
    {
        _fileNames.Add(name);
        var path = Path.Combine(_sourceDirectory.FullName, name);
        return File.WriteAllTextAsync(path, source, cancellationToken);
    }

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

    private async Task RunCommandAsync(
        IShellContext shell,
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shell);

        var result = await shell.Command.ExecuteCommandLineTool(
                new GenericCommandLineToolOptions(fileName) { Arguments = arguments, },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}