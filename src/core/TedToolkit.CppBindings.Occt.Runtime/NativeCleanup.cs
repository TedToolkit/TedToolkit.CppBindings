// -----------------------------------------------------------------------
// <copyright file="NativeCleanup.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace TedToolkit.CppBindings.Occt;

/// <summary>
/// Keeps the unmanaged cleanup call outside the generic finalizable owner type.
/// </summary>
internal static unsafe class NativeCleanup
{
    /// <summary>
    /// Invokes the non-throwing cdecl release callback for one native address.
    /// </summary>
    /// <param name="cleanup">The release callback address.</param>
    /// <param name="value">The owned native object address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Invoke(in nint cleanup, in nint value)
    {
        ((delegate* unmanaged[Cdecl]<nint, void>)cleanup)(value);
    }
}