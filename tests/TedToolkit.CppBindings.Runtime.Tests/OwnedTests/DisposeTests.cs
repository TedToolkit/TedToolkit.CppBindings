// -----------------------------------------------------------------------
// <copyright file="DisposeTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies deterministic and finalizer-driven Owned destruction.
/// </summary>
internal sealed class DisposeTests
{
    /// <summary>
    /// Verifies that managed aliases and repeated disposal destroy the contained object once.
    /// </summary>
    /// <returns>A task that completes when the destruction assertions finish.</returns>
    [Test]
    public async Task Should_destroy_once_when_aliases_are_disposed_repeatedly_Async()
    {
        await Assert.That(DisposeAliasesRepeatedly()).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that an abandoned owner destroys its contained object during finalization.
    /// </summary>
    /// <returns>A task that completes when the finalizer assertions finish.</returns>
    [Test]
    public async Task Should_destroy_once_when_an_owner_is_abandoned_Async()
    {
        await Assert.That(FinalizeAbandonedOwner()).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that deterministic disposal suppresses later finalizer destruction.
    /// </summary>
    /// <returns>A task that completes when the finalization assertions finish.</returns>
    [Test]
    public async Task Should_not_destroy_again_after_a_disposed_owner_is_collected_Async()
    {
        var observation = CollectDisposedOwner();

        await Assert.That(observation.DestroyCount).IsEqualTo(1);
        await Assert.That(observation.OwnerIsAlive).IsFalse();
    }

    private static unsafe int DisposeAliasesRepeatedly()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var owner = ControlledRaiiFactory.Create(7, destroyCount);
            var alias = owner;

            owner.Dispose();
            alias.Dispose();
            owner.Dispose();
            return *destroyCount;
        }
        finally
        {
            NativeMemory.Free(destroyCount);
        }
    }

    private static unsafe int FinalizeAbandonedOwner()
    {
        var destroyCount = CreateAbandonedOwner();

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return *destroyCount;
        }
        finally
        {
            NativeMemory.Free(destroyCount);
        }
    }

    private static unsafe (int DestroyCount, bool OwnerIsAlive) CollectDisposedOwner()
    {
        var observation = CreateDisposedOwner();

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return (*(int*)observation.DestroyCount, observation.Owner.IsAlive);
        }
        finally
        {
            NativeMemory.Free((void*)observation.DestroyCount);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe (WeakReference Owner, nint DestroyCount) CreateDisposedOwner()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var owner = ControlledRaiiFactory.Create(7, destroyCount);
        owner.Dispose();
        return (new(owner), (nint)destroyCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The test intentionally abandons this owner to verify finalizer destruction.")]
    private static unsafe int* CreateAbandonedOwner()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        _ = ControlledRaiiFactory.Create(7, destroyCount);
        return destroyCount;
    }
}