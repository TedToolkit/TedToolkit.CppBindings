// -----------------------------------------------------------------------
// <copyright file="ConstructorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.HandleTests;

/// <summary>
/// Verifies Handle construction and ownership adoption.
/// </summary>
internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that a null target is rejected before an owner is created.
    /// </summary>
    /// <returns>A task that completes when the construction assertion finishes.</returns>
    [Test]
    public async Task Should_reject_a_null_target_before_ownership_Async()
    {
        await Assert.That(CreateWithNullTarget)
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that a null release function is rejected without consuming the target.
    /// </summary>
    /// <returns>A task that completes when the construction assertion finishes.</returns>
    [Test]
    public async Task Should_reject_a_null_release_function_before_ownership_Async()
    {
        await Assert.That(CreateWithNullRelease)
            .Throws<ArgumentNullException>();
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Invalid construction must not create an owner to dispose.")]
    private static unsafe void CreateWithNullTarget()
    {
        var handle = new Handle<ControlledTransient>(null, ControlledTransientFactory.ReleaseFunction);
        GC.KeepAlive(handle);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Invalid construction must not create an owner to dispose.")]
    private static unsafe void CreateWithNullRelease()
    {
        var target = (ControlledTransient*)NativeMemory.Alloc((nuint)sizeof(ControlledTransient));
        delegate* unmanaged[Cdecl]<ControlledTransient*, void> release = null;

        try
        {
            var handle = new Handle<ControlledTransient>(target, release);
            GC.KeepAlive(handle);
        }
        finally
        {
            NativeMemory.Free(target);
        }
    }
}