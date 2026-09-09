// -----------------------------------------------------------------------
// <copyright file="FclGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Fcl.Generator;

namespace TedToolkit.CppBindings.Fcl.Generator.Tests;

/// <summary>Verifies the finite FCL generation profile.</summary>
internal sealed class FclGenerationProviderTests
{
    /// <summary>Verifies deterministic complete managed/native plans.</summary>
    /// <returns>A task that completes when plan assertions finish.</returns>
    [Test]
    public async Task Should_create_byte_identical_complete_plans_Async()
    {
        var first = new FclGenerationProvider().CreatePlan();
        var second = new FclGenerationProvider().CreatePlan();

        await Assert.That(first.ProfileId).IsEqualTo("fcl-0.7.0-obbrss-double-windows-v1");
        await Assert.That(first.NativeFunctions.Count).IsEqualTo(7);
        await Assert.That(first.NativeFunctions).IsEquivalentTo(second.NativeFunctions);
        await Assert.That(first.ManagedSources.Keys).IsEquivalentTo(second.ManagedSources.Keys);
        await Assert.That(first.NativeSources.Keys).IsEquivalentTo(second.NativeSources.Keys);
        foreach (var source in first.ManagedSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.ManagedSources[source.Key]);
        }

        foreach (var source in first.NativeSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.NativeSources[source.Key]);
        }

        var managed = first.ManagedSources["Fcl.Bindings.g.cs"];
        var native = first.NativeSources["FclProfileAdapter.cpp"];
        await Assert.That(managed).Contains("ReadOnlySpan<nuint> triangleIndices");
        await Assert.That(managed).Contains("public readonly struct FclVector3");
        await Assert.That(managed).Contains("GC.SuppressFinalize(owner);");
        await Assert.That(native).Contains("static_assert(sizeof(FclVector3Transport) == 24");
        await Assert.That(native).Contains("request.ccd_motion_type = fcl::CCDM_LINEAR");
        await Assert.That(native).Contains("request.ccd_solver_type = fcl::CCDC_CONSERVATIVE_ADVANCEMENT");
        await Assert.That(native).Contains("fallbackRequest.ccd_motion_type = fcl::CCDM_TRANS");
        await Assert.That(native).Contains("fallbackRequest.ccd_solver_type = fcl::CCDC_POLYNOMIAL_SOLVER");
        await Assert.That(native).Contains("SetError(error, 3, \"std::underflow_error\"");
        await Assert.That(native).Contains("SetError(error, 8, \"std::exception\"");
        await Assert.That(native).DoesNotContain("SetError(error, 9, \"std::exception\"");
    }

    /// <summary>Verifies every locked BVH status name and value.</summary>
    /// <returns>A task that completes when status assertions finish.</returns>
    [Test]
    public async Task Should_lock_every_bvh_return_code_Async()
    {
        var expected = new Dictionary<string, int>()
        {
            ["BVH_OK"] = 0,
            ["BVH_ERR_MODEL_OUT_OF_MEMORY"] = -1,
            ["BVH_ERR_BUILD_OUT_OF_SEQUENCE"] = -2,
            ["BVH_ERR_BUILD_EMPTY_MODEL"] = -3,
            ["BVH_ERR_BUILD_EMPTY_PREVIOUS_FRAME"] = -4,
            ["BVH_ERR_UNSUPPORTED_FUNCTION"] = -5,
            ["BVH_ERR_UNUPDATED_MODEL"] = -6,
            ["BVH_ERR_INCORRECT_DATA"] = -7,
            ["BVH_ERR_UNKNOWN"] = -8,
        };
        var actual = new FclProfile().BvhReturnCodes;

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        foreach (var item in expected)
        {
            await Assert.That(actual.ContainsKey(item.Key)).IsTrue();
            await Assert.That(actual[item.Key]).IsEqualTo(item.Value);
        }
    }
}
