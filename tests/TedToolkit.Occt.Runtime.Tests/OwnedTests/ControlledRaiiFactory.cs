// -----------------------------------------------------------------------
// <copyright file="ControlledRaiiFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.OwnedTests;

/// <summary>
/// Creates controlled RAII owners for lifetime tests.
/// </summary>
internal static unsafe class ControlledRaiiFactory
{
    /// <summary>
    /// Gets the direct unmanaged destructor used by the fixture.
    /// </summary>
    internal static delegate* unmanaged[Cdecl]<ControlledRaii*, void> DestroyFunction
    {
        get
        {
            return &Destroy;
        }
    }

    /// <summary>
    /// Placement-constructs one controlled RAII value in an owner.
    /// </summary>
    /// <param name="value">The initial fixture value.</param>
    /// <param name="destroyCount">The counter updated by destruction.</param>
    /// <returns>The constructed owner.</returns>
    [SuppressMessage(
        "Usage",
        "CA1816:Dispose methods should call SuppressFinalize",
        Justification = "The generated-shape fixture suppresses an unreturned owner after construction failure.")]
    internal static Owned<ControlledRaii> Create(int value, int* destroyCount)
    {
        ArgumentNullException.ThrowIfNull(destroyCount);

        var owner = new Owned<ControlledRaii>(&Destroy);
        var constructionSucceeded = false;

        try
        {
            fixed (ControlledRaii* target = &owner.Value)
            {
                target->Value = value;
                target->DestroyCount = destroyCount;
            }

            constructionSucceeded = true;
            GC.KeepAlive(owner);
            return owner;
        }
        finally
        {
            if (!constructionSucceeded)
            {
                GC.SuppressFinalize(owner);
            }
        }
    }

    /// <summary>
    /// Explicitly copies one controlled value into an independent owner.
    /// </summary>
    /// <param name="source">The live source owner.</param>
    /// <param name="destroyCount">The counter updated by clone destruction.</param>
    /// <returns>The independently owned copy.</returns>
    internal static Owned<ControlledRaii> Clone(Owned<ControlledRaii> source, int* destroyCount)
    {
        var value = source.Value.Value;
        GC.KeepAlive(source);
        return Create(value, destroyCount);
    }

    /// <summary>
    /// Simulates native construction failure after the destination field has been addressed.
    /// </summary>
    /// <param name="destroyCount">The counter that must remain unchanged.</param>
    /// <exception cref="InvalidOperationException">Always thrown to represent native failure.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "Usage",
        "CA1816:Dispose methods should call SuppressFinalize",
        Justification = "The fixture verifies required suppression of an unreturned owner after construction failure.")]
    internal static void FailAfterWritingConstructionTarget(int* destroyCount)
    {
        var owner = new Owned<ControlledRaii>(&Destroy);

        try
        {
            fixed (ControlledRaii* target = &owner.Value)
            {
                target->Value = 31;
                target->DestroyCount = destroyCount;
            }

            throw new InvalidOperationException("Controlled construction failure.");
        }
        finally
        {
            GC.SuppressFinalize(owner);
            GC.KeepAlive(owner);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static void Destroy(ControlledRaii* target)
    {
        Interlocked.Increment(ref *target->DestroyCount);
    }
}