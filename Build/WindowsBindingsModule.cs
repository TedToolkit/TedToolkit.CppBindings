// -----------------------------------------------------------------------
// <copyright file="WindowsBindingsModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Configuration;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Modules;
using TedToolkit.ModularPipelines.Options;

/// <summary>
/// Prepares every generated Windows provider before the shared solution-build timeout starts.
/// </summary>
/// <param name="files">The repository files.</param>
/// <param name="options">The build configuration.</param>
public sealed class WindowsBindingsModule(
    PipelineFiles files,
    IOptions<DotNetPipelineOptions> options) : CompileModule<bool>
{
    /// <inheritdoc />
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration.Create().WithTimeout(TimeSpan.FromHours(3)).WithRetryCount(0).Build();
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        var root = files.Solution.Directory
            ?? throw new InvalidOperationException("The repository root could not be resolved.");
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(vcpkgRoot))
        {
            throw new InvalidOperationException("VCPKG_ROOT is required to generate Windows bindings.");
        }

        var configuration = options.Value.Configuration;
        await BuildProcess.RunAsync(
                "pwsh",
                ["-NoProfile", "-File", Path.Combine(root.FullName, "Build", "VerifyGenerationCache.ps1"),],
                root.FullName,
                cancellationToken)
            .ConfigureAwait(false);

        var windowsProjects = new[]
        {
            Path.Combine(
                root.FullName,
                "src", "providers", "occt", "TedToolkit.CppBindings.Occt.Windows",
                "TedToolkit.CppBindings.Occt.Windows.csproj"),
            Path.Combine(
                root.FullName,
                "src", "providers", "cgal", "TedToolkit.CppBindings.Cgal.Windows",
                "TedToolkit.CppBindings.Cgal.Windows.csproj"),
            Path.Combine(
                root.FullName,
                "src", "providers", "manifold", "TedToolkit.CppBindings.Manifold.Windows",
                "TedToolkit.CppBindings.Manifold.Windows.csproj"),
            Path.Combine(
                root.FullName,
                "src", "providers", "fcl", "TedToolkit.CppBindings.Fcl.Windows",
                "TedToolkit.CppBindings.Fcl.Windows.csproj"),
        };
        foreach (var project in windowsProjects)
        {
            await BuildProcess.RunAsync(
                    "dotnet",
                    [
                        "build", project,
                        "-c", configuration,
                        "--disable-build-servers",
                        "--maxcpucount:1",
                        "-p:GeneratePackageOnBuild=false",
                        "-p:NuGetAudit=false",
                        $"-p:VcpkgRoot={vcpkgRoot}",
                    ],
                    root.FullName,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }
}
