// -----------------------------------------------------------------------
// <copyright file="ShouldSkipTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Tests.Modules.ParseModuleTests;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Modules.RequiresRealOcctAttributeTests;

/// <summary>
/// <see cref="RequiresRealOcctAttribute.ShouldSkip"/> behavior.
/// </summary>
internal sealed class ShouldSkipTests
{
    /// <summary>
    /// Verifies the real boundary is skipped when VCPKG_ROOT is not configured.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_skip_when_vcpkg_root_is_not_configured_Async()
    {
        using var fixture = VcpkgRootFixture.Create(false);
        Environment.SetEnvironmentVariable("VCPKG_ROOT", null);

        var shouldSkip = await new RequiresRealOcctAttribute().ShouldSkip(null!).ConfigureAwait(false);

        await Assert.That(shouldSkip).IsTrue();
    }

    /// <summary>
    /// Verifies the real boundary runs when the configured vcpkg root contains the target header.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_run_when_configured_root_contains_target_header_Async()
    {
        using var fixture = VcpkgRootFixture.Create(true);

        var shouldSkip = await new RequiresRealOcctAttribute().ShouldSkip(null!).ConfigureAwait(false);

        await Assert.That(shouldSkip).IsFalse();
    }

    /// <summary>
    /// Verifies the real boundary is skipped when the configured vcpkg root lacks the target header.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_skip_when_configured_root_lacks_target_header_Async()
    {
        using var fixture = VcpkgRootFixture.Create(false);

        var shouldSkip = await new RequiresRealOcctAttribute().ShouldSkip(null!).ConfigureAwait(false);

        await Assert.That(shouldSkip).IsTrue();
    }

    private sealed class VcpkgRootFixture : IDisposable
    {
        private readonly string? _originalRoot;

        private VcpkgRootFixture(DirectoryInfo root, string? originalRoot)
        {
            Root = root;
            _originalRoot = originalRoot;
        }

        public DirectoryInfo Root { get; }

        public static VcpkgRootFixture Create(bool includeTargetHeader)
        {
            var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var occtIncludeRoot = Directory.CreateDirectory(
                Path.Combine(root.FullName, "installed", "fixture-triplet", "include", "opencascade"));
            if (includeTargetHeader)
            {
                File.WriteAllText(
                    Path.Combine(occtIncludeRoot.FullName, "Geom2d_BSplineCurve.hxx"),
                    "");
            }

            var originalRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
            Environment.SetEnvironmentVariable("VCPKG_ROOT", root.FullName);
            return new(root, originalRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("VCPKG_ROOT", _originalRoot);
            Root.Delete(true);
        }
    }
}