// -----------------------------------------------------------------------
// <copyright file="NativeLibraryVersionSelectionTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Fcl.Generator;

namespace TedToolkit.CppBindings.Fcl.Generator.Tests;

/// <summary>Verifies FCL native library version selection behavior.</summary>
internal sealed class NativeLibraryVersionSelectionTests
{
    /// <summary>Verifies omitted selection preserves the original unversioned CMake requirement.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Test]
    public async Task Should_preserve_the_unversioned_package_requirement_Async()
    {
        var vcpkgRoot = new DirectoryInfo(
            Environment.GetEnvironmentVariable("VCPKG_ROOT") is { Length: > 0, } root ? root : @"C:\vcpkg");
        var plan = await new FclGenerationProvider(vcpkgRoot)
            .CreatePlanAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var source = plan.CppSources.Single(static item => item.RelativePath == "CMakeLists.txt");
        var writer = new StringWriter();
        await using (writer.ConfigureAwait(false))
        {
            await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
        }

        await Assert.That(writer.ToString()).Contains("find_package(fcl CONFIG REQUIRED)");
        await Assert.That(writer.ToString()).DoesNotContain(" EXACT ");
    }
}
