// -----------------------------------------------------------------------
// <copyright file="StableAddressTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies address-sensitive native state across collection and destruction.
/// </summary>
internal sealed class StableAddressTests
{
    /// <summary>
    /// Verifies that native self-pointers survive compacting collection and repeated disposal.
    /// </summary>
    /// <returns>A task that completes when the address and destruction assertions finish.</returns>
    [Test]
    public async Task Should_preserve_the_construction_address_until_disposal_Async()
    {
        var observation = CollectAndDispose();

        await Assert.That(observation.AddressPreserved).IsTrue();
        await Assert.That(observation.DestroyCount).IsEqualTo(1);
        await Assert.That(observation.AddressMismatch).IsEqualTo(0);
    }

    /// <summary>
    /// Verifies that pinned storage does not root its abandoned owner or bypass native destruction.
    /// </summary>
    /// <returns>A task that completes when the finalization assertions finish.</returns>
    [Test]
    public async Task Should_finalize_an_abandoned_address_sensitive_owner_Async()
    {
        var observation = CollectAbandonedOwner();

        await Assert.That(observation.OwnerIsAlive).IsFalse();
        await Assert.That(observation.DestroyCount).IsEqualTo(1);
        await Assert.That(observation.AddressMismatch).IsEqualTo(0);
    }

    private static unsafe (bool AddressPreserved, int DestroyCount, int AddressMismatch) CollectAndDispose()
    {
        var counters = (int*)NativeMemory.AllocZeroed(2, (nuint)sizeof(int));

        try
        {
            using var owner = Create(counters);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            var addressPreserved = Address(owner) == owner.Value.ConstructionAddress;
            owner.Dispose();
            owner.Dispose();
            return (addressPreserved, counters[0], counters[1]);
        }
        finally
        {
            NativeMemory.Free(counters);
        }
    }

    private static unsafe (bool OwnerIsAlive, int DestroyCount, int AddressMismatch) CollectAbandonedOwner()
    {
        var counters = (int*)NativeMemory.AllocZeroed(2, (nuint)sizeof(int));

        try
        {
            var reference = Abandon(counters);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return (reference.IsAlive, counters[0], counters[1]);
        }
        finally
        {
            NativeMemory.Free(counters);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe Owned<StableRaii> Create(int* counters)
    {
        var owner = new Owned<StableRaii>(&Destroy);
        fixed (StableRaii* target = &owner.Value)
        {
            target->ConstructionAddress = (nint)target;
            target->Counters = counters;
        }

        GC.KeepAlive(owner);
        return owner;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The test abandons this owner to verify that pinned storage permits finalization.")]
    private static unsafe WeakReference Abandon(int* counters)
    {
        return new(Create(counters));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe nint Address(Owned<StableRaii> owner)
    {
        fixed (StableRaii* target = &owner.Value)
        {
            return (nint)target;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static unsafe void Destroy(StableRaii* target)
    {
        Interlocked.Increment(ref target->Counters[0]);
        if ((nint)target == target->ConstructionAddress)
        {
            return;
        }

        Interlocked.Increment(ref target->Counters[1]);
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct StableRaii : ICppStableAddress
    {
        public nint ConstructionAddress;

        public int* Counters;
    }
}