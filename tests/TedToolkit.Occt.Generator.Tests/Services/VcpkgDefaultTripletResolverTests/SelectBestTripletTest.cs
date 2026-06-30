// -----------------------------------------------------------------------
// <copyright file="SelectBestTripletTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt.Generator.Services;

namespace TedToolkit.Occt.Generator.Tests.Services.VcpkgDefaultTripletResolverTests;

/// <summary>
/// Verifies triplet selection for the current platform.
/// </summary>
internal sealed class SelectBestTripletTest
{
    /// <summary>
    /// Verifies an exact host triplet is preferred.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_prefer_host_platform_triplet_when_multiple_occt_triplets_are_installed_Async()
    {
        var triplet = VcpkgDefaultTripletResolver.SelectBestTriplet(
            ["arm64-osx", "x64-linux", "x64-windows",],
            OSPlatform.Windows,
            Architecture.X64);

        await Assert.That(triplet).IsEqualTo("x64-windows");
    }

    /// <summary>
    /// Verifies a compatible host-family triplet is used when the exact triplet is absent.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_fallback_to_compatible_host_family_when_exact_triplet_is_missing_Async()
    {
        var triplet = VcpkgDefaultTripletResolver.SelectBestTriplet(
            ["x64-windows-static", "x64-linux",],
            OSPlatform.Windows,
            Architecture.X64);

        await Assert.That(triplet).IsEqualTo("x64-windows-static");
    }
}