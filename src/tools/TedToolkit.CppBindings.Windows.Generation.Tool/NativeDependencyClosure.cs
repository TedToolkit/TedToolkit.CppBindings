// -----------------------------------------------------------------------
// <copyright file="NativeDependencyClosure.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal interface INativeDependencyClosure
{
    Task<bool> IsCompleteAsync(
        string modulePath,
        string nativeLibrary,
        string destination,
        string manifest,
        string workingDirectory,
        CancellationToken cancellationToken);

    Task StageAsync(
        string modulePath,
        string compiler,
        string nativeLibrary,
        string destination,
        string vcpkgBin,
        string ownedRoot,
        string workingDirectory,
        CancellationToken cancellationToken);
}

internal sealed class PowerShellNativeDependencyClosure(IProcessRunner processes) : INativeDependencyClosure
{
    internal PowerShellNativeDependencyClosure()
        : this(new SystemProcessRunner())
    {
    }

    public async Task<bool> IsCompleteAsync(
        string modulePath,
        string nativeLibrary,
        string destination,
        string manifest,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        const string command = "$ErrorActionPreference = 'Stop'; Import-Module $args[0] -Force; "
                               + "if (Test-NativeDependencyClosure -NativeLibrary $args[1] "
                               + "-Destination $args[2] -Manifest $args[3]) { exit 0 } else { exit 3 }";
        var result = await processes.RunAsync(
                "pwsh",
                ["-NoProfile", "-CommandWithArgs", command, modulePath, nativeLibrary, destination, manifest,],
                workingDirectory,
                null,
                throwOnError: false,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode is 0;
    }

    public async Task StageAsync(
        string modulePath,
        string compiler,
        string nativeLibrary,
        string destination,
        string vcpkgBin,
        string ownedRoot,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        const string command = "$ErrorActionPreference = 'Stop'; Import-Module $args[0] -Force; "
                               + "$toolchain = Get-WindowsNativeToolchain -Compiler $args[1]; "
                               + "Set-NativeDependencyClosure -NativeLibrary $args[2] "
                               + "-Destination $args[3] -VcpkgBin $args[4] "
                               + "-Toolchain $toolchain -OwnedRoot $args[5] | Out-Null";
        _ = await processes.RunAsync(
                "pwsh",
                [
                    "-NoProfile", "-CommandWithArgs", command, modulePath, compiler, nativeLibrary,
                    destination, vcpkgBin, ownedRoot,
                ],
                workingDirectory,
                null,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
    }
}