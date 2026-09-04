// -----------------------------------------------------------------------
// <copyright file="NativeIntegrationModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Modules;

/// <summary>
/// Builds and executes the native Handle integration fixtures.
/// </summary>
[DependsOn<DotnetBuildModule>]
public sealed class NativeIntegrationModule(PipelineFiles files) : CompileCheckModule<bool>
{
    /// <inheritdoc />
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration.Create().WithRetryCount(0).Build();
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = files.Solution.Directory
            ?? throw new InvalidOperationException("The repository root could not be resolved.");
        var sourceDirectory = Path.Combine(repositoryRoot.FullName, "tests", "native", "handle-fixtures");
        var buildDirectory = Path.Combine(repositoryRoot.FullName, "output", "tests", "handle-fixtures");
        var configureArguments = new List<string>()
        {
            "--fresh", "-S", sourceDirectory, "-B", buildDirectory, "-DVCPKG_APPLOCAL_DEPS=OFF",
        };
        var windowsToolchain = FindWindowsToolchain();
        if (windowsToolchain is not null)
        {
            configureArguments.AddRange(
            [
                "-G", "Ninja",
                $"-DCMAKE_MAKE_PROGRAM={windowsToolchain.Value.Ninja}",
                $"-DCMAKE_CXX_COMPILER={windowsToolchain.Value.Compiler}",
            ]);
        }

        await BuildProcess.RunAsync(
                "cmake",
                configureArguments,
                repositoryRoot.FullName,
                cancellationToken)
            .ConfigureAwait(false);
        await BuildProcess.RunAsync(
                "cmake",
                ["--build", buildDirectory, "--config", "Release",],
                repositoryRoot.FullName,
                cancellationToken)
            .ConfigureAwait(false);
        await BuildProcess.RunAsync(
                "ctest",
                ["--test-dir", buildDirectory, "-C", "Release", "--output-on-failure",],
                repositoryRoot.FullName,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static (string Ninja, string Compiler)? FindWindowsToolchain()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var visualStudioRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Microsoft Visual Studio");
        if (!Directory.Exists(visualStudioRoot))
        {
            throw new InvalidOperationException("A Visual Studio C++ toolchain is required for native integration tests.");
        }

        foreach (var installation in Directory.EnumerateDirectories(visualStudioRoot)
                     .SelectMany(Directory.EnumerateDirectories)
                     .OrderByDescending(static directory => directory, StringComparer.OrdinalIgnoreCase))
        {
            var ninja = Path.Combine(
                installation,
                "Common7", "IDE", "CommonExtensions", "Microsoft", "CMake", "Ninja", "ninja.exe");
            var compiler = Path.Combine(
                installation,
                "VC", "Tools", "Llvm", "x64", "bin", "clang-cl.exe");
            if (File.Exists(ninja) && File.Exists(compiler))
            {
                return (ninja, compiler);
            }
        }

        throw new InvalidOperationException(
            "Visual Studio with the C++ CMake and LLVM components is required for native integration tests.");
    }
}
