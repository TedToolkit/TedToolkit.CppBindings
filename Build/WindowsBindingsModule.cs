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
/// Prepares the expensive generated native artifact before the shared solution-build timeout starts.
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
        var root = files.Solution.Directory
            ?? throw new InvalidOperationException("The repository root could not be resolved.");
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(vcpkgRoot))
        {
            throw new InvalidOperationException("VCPKG_ROOT is required to generate Windows bindings.");
        }

        var configuration = options.Value.Configuration;
        var hostDirectory = Path.Combine(root.FullName, "tests", "TedToolkit.Occt.Console");
        await BuildProcess.RunAsync(
                "pwsh",
                ["-NoProfile", "-File", Path.Combine(root.FullName, "Build", "VerifyGenerationCache.ps1"),],
                root.FullName,
                cancellationToken)
            .ConfigureAwait(false);
        await BuildProcess.RunAsync(
                "dotnet",
                ["build", Path.Combine(hostDirectory, "TedToolkit.Occt.Console.csproj"), "-c", configuration,],
                root.FullName,
                cancellationToken)
            .ConfigureAwait(false);
        await BuildProcess.RunAsync(
                "pwsh",
                [
                    "-NoProfile", "-File", Path.Combine(root.FullName, "Build", "GenerateWindowsBindings.ps1"),
                    "-RepositoryRoot", root.FullName,
                    "-GeneratorHost", Path.Combine(hostDirectory, "bin", configuration, "net10.0", "TedToolkit.Occt.Console.dll"),
                    "-GeneratedRoot", Path.Combine(root.FullName, "output", "generated"),
                    "-VcpkgRoot", vcpkgRoot,
                    "-Configuration", configuration,
                ],
                root.FullName,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
