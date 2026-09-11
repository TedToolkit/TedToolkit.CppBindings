// -----------------------------------------------------------------------
// <copyright file="ValueTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Runtime.Tests.HandleTests;

/// <summary>
/// Verifies direct non-owning access to a Handle target.
/// </summary>
internal sealed class ValueTests
{
    /// <summary>
    /// Verifies that Value aliases live native storage without copying it.
    /// </summary>
    /// <returns>A task that completes when the aliasing assertions finish.</returns>
    [Test]
    public async Task Should_alias_live_native_storage_without_copying_Async()
    {
        var observation = ObserveLiveValue();

        await Assert.That(observation.Value).IsEqualTo(19);
        await Assert.That(observation.ReleaseCountBeforeDispose).IsEqualTo(0);
        await Assert.That(observation.ReleaseCountAfterDispose).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Value rejects access after its owner is disposed.
    /// </summary>
    /// <returns>A task that completes when the disposed-state assertion finishes.</returns>
    [Test]
    public async Task Should_throw_before_dereferencing_after_disposal_Async()
    {
        await Assert.That(ReadDisposedValue).Throws<ObjectDisposedException>();
    }

    private static unsafe (int Value, int ReleaseCountBeforeDispose, int ReleaseCountAfterDispose) ObserveLiveValue()
    {
        var releaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var handle = ControlledTransientFactory.Create(7, releaseCount);
            handle.Value.Value = 19;

            var value = handle.Value.Value;
            var countBeforeDispose = *releaseCount;
            handle.Dispose();
            return (value, countBeforeDispose, *releaseCount);
        }
        finally
        {
            NativeMemory.Free(releaseCount);
        }
    }

    private static unsafe void ReadDisposedValue()
    {
        var releaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var handle = ControlledTransientFactory.Create(7, releaseCount);
            handle.Dispose();
            _ = handle.Value.Value;
        }
        finally
        {
            NativeMemory.Free(releaseCount);
        }
    }
}