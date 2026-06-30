// -----------------------------------------------------------------------
// <copyright file="GetResolvedValuesTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.Options.GenerationOptionsTests;

/// <summary>
/// Verifies <see cref="GenerationOptions"/> resolves configured values correctly.
/// </summary>
internal sealed class GetResolvedValuesTest
{
    /// <summary>
    /// Verifies an explicitly configured triplet bypasses the default resolver.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_return_user_triplet_without_resolver_when_triplet_is_configured_Async()
    {
        var options = CreateOptions() with { Triplet = "arm64-osx", };
        var resolver = new SpyVcpkgDefaultTripletResolver();

        var triplet = options.GetTriplet(resolver);

        await Assert.That(triplet).IsEqualTo("arm64-osx");
        await Assert.That(resolver.TripletCallCount).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies the default resolver is used when no triplet is configured.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_resolve_triplet_when_triplet_is_not_configured_Async()
    {
        var options = CreateOptions();
        var resolver = new SpyVcpkgDefaultTripletResolver() { Triplet = "x64-windows-static", };

        var triplet = options.GetTriplet(resolver);

        await Assert.That(triplet).IsEqualTo("x64-windows-static");
        await Assert.That(resolver.TripletCallCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies an explicitly configured C++ version is preserved.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_return_user_cpp_version_when_cpp_version_is_configured_Async()
    {
        var options = CreateOptions() with { CppVersion = 23, };

        var version = options.CppVersion;

        await Assert.That(version).IsEqualTo(23);
    }

    /// <summary>
    /// Verifies the default C++ version remains C++17 when unspecified.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_return_cpp17_when_cpp_version_is_not_configured_Async()
    {
        var options = CreateOptions();

        var version = options.CppVersion;

        await Assert.That(version).IsEqualTo(17);
    }

    private static GenerationOptions CreateOptions()
    {
        return new()
        {
            DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
        };
    }

    private sealed class SpyVcpkgDefaultTripletResolver : IVcpkgDefaultTripletResolver
    {
        public string Triplet { get; init; } = "x64-windows";

        public int TripletCallCount { get; private set; }

        public string GetTriplet()
        {
            TripletCallCount++;
            return Triplet;
        }
    }
}