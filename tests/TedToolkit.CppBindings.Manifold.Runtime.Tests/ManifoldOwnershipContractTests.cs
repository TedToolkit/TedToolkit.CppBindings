// -----------------------------------------------------------------------
// <copyright file="ManifoldOwnershipContractTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Manifold.Runtime.Tests;

/// <summary>Verifies the Owned lifetime contract used by generated Manifold values.</summary>
[NotInParallel("manifold-runtime-owner")]
internal sealed class ManifoldOwnershipContractTests
{
    private static int destroyCount;

    /// <summary>Verifies concurrent disposal destroys once and rejects later use.</summary>
    /// <returns>A task that completes when ownership behavior is checked.</returns>
    [Test]
    public async Task Should_destroy_once_under_concurrent_disposal_and_reject_later_use_Async()
    {
        destroyCount = 0;
        var owner = CreateOwner();

        Parallel.For(0, 32, _ => owner.Dispose());

        await Assert.That(destroyCount).IsEqualTo(1);
        await Assert.That(() => ReadValue(owner)).Throws<ObjectDisposedException>();
    }

    /// <summary>Verifies finalization destroys a successfully created owner exactly once.</summary>
    /// <returns>A task that completes when finalization is observed.</returns>
    [Test]
    public async Task Should_destroy_a_finalized_owner_exactly_once_Async()
    {
        destroyCount = 0;
        var owner = AbandonOwner();

        for (var attempt = 0; attempt < 5 && owner.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        await Assert.That(owner.IsAlive).IsFalse();
        await Assert.That(destroyCount).IsEqualTo(1);
    }

    /// <summary>Verifies suppressed incomplete construction is never destroyed.</summary>
    /// <returns>A task that completes when incomplete ownership is checked.</returns>
    [Test]
    public async Task Should_not_destroy_an_incomplete_owner_Async()
    {
        destroyCount = 0;
        var owner = AbandonSuppressedOwner();

        for (var attempt = 0; attempt < 5 && owner.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        await Assert.That(owner.IsAlive).IsFalse();
        await Assert.That(destroyCount).IsEqualTo(0);
    }

    private static unsafe global::TedToolkit.CppBindings.Owned<ManifoldLifetimeProbe> CreateOwner()
    {
        return new(&Destroy);
    }

    private static int ReadValue(global::TedToolkit.CppBindings.Owned<ManifoldLifetimeProbe> owner)
    {
        return owner.Value.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonOwner()
    {
        return new(CreateOwner());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonSuppressedOwner()
    {
        var owner = CreateOwner();
        GC.SuppressFinalize(owner);
        return new(owner);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Destroy(ManifoldLifetimeProbe* value)
    {
        Interlocked.Increment(ref destroyCount);
    }
}

internal struct ManifoldLifetimeProbe : global::TedToolkit.CppBindings.ICppRaii
{
    internal int Value;
}
