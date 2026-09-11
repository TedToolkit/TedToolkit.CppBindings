// -----------------------------------------------------------------------
// <copyright file="ControlledTransientFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.HandleTests;

/// <summary>
/// Creates controlled transient owners for lifetime tests.
/// </summary>
internal static unsafe class ControlledTransientFactory
{
    /// <summary>
    /// Gets the direct unmanaged release function used by the fixture.
    /// </summary>
    internal static delegate* unmanaged[Cdecl]<ControlledTransient*, void> ReleaseFunction
    {
        get
        {
            return &Release;
        }
    }

    /// <summary>
    /// Creates an owner over newly allocated fixture storage.
    /// </summary>
    /// <param name="value">The initial fixture value.</param>
    /// <param name="releaseCount">The counter updated by release.</param>
    /// <returns>The new owner.</returns>
    internal static Handle<ControlledTransient> Create(int value, int* releaseCount)
    {
        var target = (ControlledTransient*)NativeMemory.Alloc((nuint)sizeof(ControlledTransient));
        target->Value = value;
        target->ReleaseCount = releaseCount;
        return new(target, &Release);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static void Release(ControlledTransient* target)
    {
        Interlocked.Increment(ref *target->ReleaseCount);
        NativeMemory.Free(target);
    }
}