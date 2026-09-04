// -----------------------------------------------------------------------
// <copyright file="ValueTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies direct non-owning access to an Owned value.
/// </summary>
internal sealed class ValueTests
{
    /// <summary>
    /// Verifies that Value aliases the contained field without copying it.
    /// </summary>
    /// <returns>A task that completes when the aliasing assertions finish.</returns>
    [Test]
    public async Task Should_alias_the_contained_field_without_copying_Async()
    {
        var observation = ObserveLiveValue();

        await Assert.That(observation.InitialValue).IsEqualTo(17);
        await Assert.That(observation.UpdatedValue).IsEqualTo(29);
        await Assert.That(observation.DestroyCount).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that Value rejects access after its owner is disposed.
    /// </summary>
    /// <returns>A task that completes when the disposed-state assertion finishes.</returns>
    [Test]
    public async Task Should_throw_before_accessing_the_field_after_disposal_Async()
    {
        var observation = ObserveDisposedValueAccess();

        await Assert.That(observation.Exception).IsTypeOf<ObjectDisposedException>();
        await Assert.That(observation.DestroyCountBeforeAccess).IsEqualTo(1);
        await Assert.That(observation.DestroyCountAfterAccess).IsEqualTo(observation.DestroyCountBeforeAccess);
    }

    private static unsafe (int InitialValue, int UpdatedValue, int DestroyCount) ObserveLiveValue()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var owner = ControlledRaiiFactory.Create(17, destroyCount);
            var initialValue = owner.Value.Value;
            owner.Value.Value = 29;
            var updatedValue = owner.Value.Value;
            owner.Dispose();
            return (initialValue, updatedValue, *destroyCount);
        }
        finally
        {
            NativeMemory.Free(destroyCount);
        }
    }

    private static unsafe (
        Exception Exception,
        int DestroyCountBeforeAccess,
        int DestroyCountAfterAccess) ObserveDisposedValueAccess()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            var owner = ControlledRaiiFactory.Create(7, destroyCount);
            owner.Dispose();
            var destroyCountBeforeAccess = *destroyCount;

            try
            {
                _ = owner.Value.Value;
                throw new InvalidOperationException("Disposed Value access unexpectedly succeeded.");
            }
            catch (ObjectDisposedException exception)
            {
                return (exception, destroyCountBeforeAccess, *destroyCount);
            }
        }
        finally
        {
            NativeMemory.Free(destroyCount);
        }
    }
}