// -----------------------------------------------------------------------
// <copyright file="CgalGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using TedToolkit.CppBindings.Cgal.Generator;
using TedToolkit.CppBindings.Generator;

namespace TedToolkit.CppBindings.Cgal.Generator.Tests;

/// <summary>
/// Verifies the locked finite CGAL profile against real installed headers.
/// </summary>
internal sealed class CgalGenerationProviderTests
{
    /// <summary>
    /// Verifies two real-header runs produce byte-identical complete inventories and paired sources.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_generate_the_locked_profile_deterministically_Async()
    {
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider();

        var firstPlan = await firstProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var secondPlan = await secondProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var firstManaged = await RenderAsync(firstPlan.CSharpSources).ConfigureAwait(false);
        var secondManaged = await RenderAsync(secondPlan.CSharpSources).ConfigureAwait(false);
        var firstNative = await RenderAsync(firstPlan.CppSources).ConfigureAwait(false);
        var secondNative = await RenderAsync(secondPlan.CppSources).ConfigureAwait(false);

        await Assert.That(firstProvider.Profile.ProfileId).IsEqualTo("epick-windows-v1");
        await Assert.That(firstProvider.Profile.CgalVersion).IsEqualTo("6.2");
        await Assert.That(firstProvider.Inventory.Sources.Count).IsEqualTo(3773);
        await Assert.That(firstProvider.Inventory.Sources.Count(static item => item.Disposition == "profile-root"))
            .IsEqualTo(7);
        await Assert.That(firstProvider.Inventory.Candidates.Count).IsEqualTo(19);
        await Assert.That(firstProvider.Inventory.Admitted.Count).IsEqualTo(19);
        await Assert.That(firstProvider.Inventory.Unsupported.Count).IsEqualTo(0);
        await Assert.That(firstProvider.Inventory.Admitted.Count + firstProvider.Inventory.Unsupported.Count)
            .IsEqualTo(firstProvider.Inventory.Candidates.Count);
        await Assert.That(Hash(firstManaged)).IsEqualTo(Hash(secondManaged));
        await Assert.That(Hash(firstNative)).IsEqualTo(Hash(secondNative));
        await Assert.That(firstPlan.NativeExports.SequenceEqual(secondPlan.NativeExports, StringComparer.Ordinal)).IsTrue();
    }

    /// <summary>
    /// Verifies the plan carries the finite API, failure boundary, build metadata, and explicit source dispositions.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_emit_matching_managed_and_native_profile_artifacts_Async()
    {
        var provider = CreateProvider();
        var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var managed = await RenderAsync(plan.CSharpSources).ConfigureAwait(false);
        var native = await RenderAsync(plan.CppSources).ConfigureAwait(false);

        await Assert.That(managed.Keys).Contains("Cgal.Generated.g.cs");
        await Assert.That(managed.Keys).Contains("source-inventory.json");
        await Assert.That(managed.Keys).Contains("candidate-inventory.json");
        await Assert.That(managed["Cgal.Generated.g.cs"]).Contains("public readonly struct Point_2");
        await Assert.That(managed["Cgal.Generated.g.cs"]).Contains("public static double SquaredDistance(Point_3");
        await Assert.That(native.Keys).Contains("Cgal.Native.cpp");
        await Assert.That(native.Keys).Contains("CMakeLists.txt");
        await Assert.That(native["Cgal.Native.cpp"]).Contains("CGAL::intersection");
        await Assert.That(native["CMakeLists.txt"])
            .Contains("target_compile_definitions(ted_toolkit_cpp_bindings_cgal PRIVATE CGAL_DEBUG)");
        await Assert.That(plan.NativeExports.Count).IsEqualTo(7);
        await Assert.That(plan.NativeExports).Contains("Cgal_NativeError_Clear");
        await Assert.That(plan.NativeExports).Contains("Cgal_Segment2_Intersection");
    }

    private static CgalGenerationProvider CreateProvider()
    {
        var root = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = @"C:\vcpkg";
        }

        return new(new()
        {
            VcpkgRoot = new(root),
            CSharpFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-managed")),
            CppFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-native")),
            CSharpNamespace = "TedToolkit.CppBindings.Cgal",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
            CppVersion = 20,
        });
    }

    private static async Task<SortedDictionary<string, string>> RenderAsync(IEnumerable<GeneratedSource> sources)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var writer = new StringWriter();
            await using (writer.ConfigureAwait(false))
            {
                await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
                result.Add(source.RelativePath, writer.ToString());
            }
        }

        return result;
    }

    private static string Hash(IReadOnlyDictionary<string, string> sources)
    {
        var canonical = string.Join("\n", sources.Select(static item => item.Key + "\n" + item.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}