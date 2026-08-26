// -----------------------------------------------------------------------
// <copyright file="FirstTransientFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.FirstWrapper;

/// <summary>
/// Provides an independently authored transient wrapper fixture.
/// </summary>
public static unsafe class FirstTransientFactory
{
    /// <summary>
    /// Creates an owned transient fixture through Runtime's public Handle contract.
    /// </summary>
    /// <param name="value">The initial fixture value.</param>
    /// <param name="releaseCount">The unmanaged counter incremented by the release entry point.</param>
    /// <returns>The managed owner of the fixture.</returns>
    public static Handle<FirstTransient> Create(int value, int* releaseCount)
    {
        ArgumentNullException.ThrowIfNull(releaseCount);

        var target = (FirstTransient*)NativeMemory.Alloc((nuint)sizeof(FirstTransient));
        target->Value = value;
        target->ReleaseCount = releaseCount;
        return new(target, &Release);
    }

    /// <summary>
    /// Creates an owned transient through a matching native fixture library.
    /// </summary>
    /// <param name="nativeLibrary">The loaded native fixture library.</param>
    /// <param name="value">The initial fixture value.</param>
    /// <param name="releaseCount">The unmanaged counter incremented by the native release export.</param>
    /// <returns>The managed owner of the native fixture.</returns>
    public static Handle<FirstTransient> Create(nint nativeLibrary, int value, int* releaseCount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(nativeLibrary);
        ArgumentNullException.ThrowIfNull(releaseCount);

        var create = (delegate* unmanaged[Cdecl]<int, int*, FirstTransient*>)NativeLibrary.GetExport(
            nativeLibrary,
            "first_transient_create");
        var release = (delegate* unmanaged[Cdecl]<FirstTransient*, void>)NativeLibrary.GetExport(
            nativeLibrary,
            "first_transient_release");
        return new(create(value, releaseCount), release);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static void Release(FirstTransient* target)
    {
        Interlocked.Increment(ref *target->ReleaseCount);
        NativeMemory.Free(target);
    }
}