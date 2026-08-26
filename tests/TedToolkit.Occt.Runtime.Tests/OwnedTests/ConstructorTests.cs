// -----------------------------------------------------------------------
// <copyright file="ConstructorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies Owned construction admission and failed-construction cleanup.
/// </summary>
internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that a null destructor is rejected before ownership begins.
    /// </summary>
    /// <returns>A task that completes when the destructor assertion finishes.</returns>
    [Test]
    public async Task Should_reject_a_null_destructor_Async()
    {
        await Assert.That(CreateWithNullDestructor).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that generated construction failure suppresses destruction of the unconstructed value.
    /// </summary>
    /// <returns>A task that completes when the failed-construction assertions finish.</returns>
    [Test]
    public async Task Should_not_destroy_an_unreturned_failed_construction_target_Async()
    {
        await Assert.That(FailConstructionAndCollect()).IsEqualTo(0);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Invalid construction must not create an owner to dispose.")]
    private static unsafe void CreateWithNullDestructor()
    {
        delegate* unmanaged[Cdecl]<ControlledRaii*, void> destroy = null;
        var owner = new Owned<ControlledRaii>(destroy);
        GC.KeepAlive(owner);
    }

    private static unsafe int FailConstructionAndCollect()
    {
        var destroyCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            try
            {
                ControlledRaiiFactory.FailAfterWritingConstructionTarget(destroyCount);
            }
            catch (InvalidOperationException)
            {
            }

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
}