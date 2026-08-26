// -----------------------------------------------------------------------
// <copyright file="DisposeTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt.Runtime.Tests.HandleTests;

/// <summary>
/// Verifies deterministic and finalizer-driven Handle release.
/// </summary>
internal sealed class DisposeTests
{
    /// <summary>
    /// Verifies that managed aliases and repeated disposal release the adopted reference once.
    /// </summary>
    /// <returns>A task that completes when the release assertions finish.</returns>
    [Test]
    public async Task Should_release_once_when_aliases_are_disposed_repeatedly_Async()
    {
        await Assert.That(DisposeAliasesRepeatedly()).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that an abandoned owner releases its adopted reference during finalization.
    /// </summary>
    /// <returns>A task that completes when the finalizer assertions finish.</returns>
    [Test]
    public async Task Should_release_once_when_an_owner_is_abandoned_Async()
    {
        await Assert.That(FinalizeAbandonedHandle()).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that deterministic disposal suppresses later finalizer release work.
    /// </summary>
    /// <returns>A task that completes when the finalization assertions finish.</returns>
    [Test]
    public async Task Should_not_release_again_after_a_disposed_owner_is_collected_Async()
    {
        var observation = CollectDisposedHandle();

        await Assert.That(observation.ReleaseCount).IsEqualTo(1);
        await Assert.That(observation.OwnerIsAlive).IsFalse();
    }

    private static unsafe int DisposeAliasesRepeatedly()
    {
        var releaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var handle = ControlledTransientFactory.Create(7, releaseCount);
            var alias = handle;

            handle.Dispose();
            alias.Dispose();
            handle.Dispose();
            return *releaseCount;
        }
        finally
        {
            NativeMemory.Free(releaseCount);
        }
    }

    private static unsafe int FinalizeAbandonedHandle()
    {
        var releaseCount = CreateAbandonedHandle();

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return *releaseCount;
        }
        finally
        {
            NativeMemory.Free(releaseCount);
        }
    }

    private static unsafe (int ReleaseCount, bool OwnerIsAlive) CollectDisposedHandle()
    {
        var observation = CreateDisposedHandle();

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return (*(int*)observation.ReleaseCount, observation.Owner.IsAlive);
        }
        finally
        {
            NativeMemory.Free((void*)observation.ReleaseCount);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe (WeakReference Owner, nint ReleaseCount) CreateDisposedHandle()
    {
        var releaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var handle = ControlledTransientFactory.Create(7, releaseCount);
        handle.Dispose();
        return (new(handle), (nint)releaseCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The test intentionally abandons this owner to verify finalizer release.")]
    private static unsafe int* CreateAbandonedHandle()
    {
        var releaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        _ = ControlledTransientFactory.Create(7, releaseCount);
        return releaseCount;
    }
}