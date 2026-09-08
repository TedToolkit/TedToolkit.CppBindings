// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Manifold.Generator;

namespace TedToolkit.CppBindings.Manifold.Generator.Tests;

/// <summary>
/// Verifies the locked finite Manifold profile and paired source renderer.
/// </summary>
internal sealed class ManifoldGenerationProviderTests
{
    /// <summary>
    /// Verifies that two plans are byte-identical and include the complete finite ABI authority.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_create_byte_identical_complete_plans_Async()
    {
        var first = new ManifoldGenerationProvider().CreatePlan();
        var second = new ManifoldGenerationProvider().CreatePlan();

        await Assert.That(first.ProfileId).IsEqualTo("manifold-3.5.2-windows-v1");
        await Assert.That(first.NativeExports.Count).IsEqualTo(9);
        await Assert.That(first.NativeExports).IsEquivalentTo(second.NativeExports);
        await Assert.That(first.ManagedSources.Keys).IsEquivalentTo(second.ManagedSources.Keys);
        await Assert.That(first.NativeSources.Keys).IsEquivalentTo(second.NativeSources.Keys);
        await Assert.That(first.ManagedSources.Keys).Contains("layout-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("ownership-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("source-declaration-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("toolchain-inventory.json");
        foreach (var source in first.ManagedSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.ManagedSources[source.Key]);
        }

        foreach (var source in first.NativeSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.NativeSources[source.Key]);
        }

        var managed = first.ManagedSources["Manifold.Bindings.g.cs"];
        var native = first.NativeSources["ManifoldProfileAdapter.cpp"];
        await Assert.That(managed).Contains("public enum ManifoldOp : sbyte");
        await Assert.That(managed).Contains("public static global::TedToolkit.CppBindings.Owned<Manifold> Create");
        await Assert.That(managed).Contains("ReadOnlySpan<double> vertexCoordinates");
        await Assert.That(managed).Contains("GC.SuppressFinalize(result);");
        await Assert.That(native).Contains("static_assert(sizeof(ManifoldAdapter) == 8");
        await Assert.That(native).Contains("Manifold::Error::Cancelled) == 14");
        await Assert.That(native).Contains("NativeApi_GetFunctionTable");
    }

    /// <summary>
    /// Verifies the profile snapshots the exact operation and status enumeration values.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_lock_native_enum_values_Async()
    {
        var profile = new ManifoldProfile();
        var expectedOperations = new Dictionary<string, int>()
        {
            ["Add"] = 0,
            ["Subtract"] = 1,
            ["Intersect"] = 2,
        };
        var expectedErrors = new Dictionary<string, int>()
        {
            ["NoError"] = 0,
            ["NonFiniteVertex"] = 1,
            ["NotManifold"] = 2,
            ["VertexOutOfBounds"] = 3,
            ["PropertiesWrongLength"] = 4,
            ["MissingPositionProperties"] = 5,
            ["MergeVectorsDifferentLengths"] = 6,
            ["MergeIndexOutOfBounds"] = 7,
            ["TransformWrongLength"] = 8,
            ["RunIndexWrongLength"] = 9,
            ["FaceIDWrongLength"] = 10,
            ["InvalidConstruction"] = 11,
            ["ResultTooLarge"] = 12,
            ["InvalidTangents"] = 13,
            ["Cancelled"] = 14,
        };

        await Assert.That(profile.Operations.Count).IsEqualTo(3);
        await Assert.That(profile.NativeOperationUnderlyingType).IsEqualTo("char");
        foreach (var expected in expectedOperations)
        {
            await Assert.That(profile.Operations.ContainsKey(expected.Key)).IsTrue();
            await Assert.That(profile.Operations[expected.Key]).IsEqualTo(expected.Value);
        }

        await Assert.That(profile.Errors.Count).IsEqualTo(15);
        await Assert.That(profile.NativeErrorUnderlyingType).IsEqualTo("int");
        foreach (var expected in expectedErrors)
        {
            await Assert.That(profile.Errors.ContainsKey(expected.Key)).IsTrue();
            await Assert.That(profile.Errors[expected.Key]).IsEqualTo(expected.Value);
        }
    }
}
