// -----------------------------------------------------------------------
// <copyright file="NativeCleanup.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace TedToolkit.CppBindings;

/// <summary>
/// Invokes native cleanup without placing a generic function-pointer signature in a finalizable type.
/// </summary>
internal static unsafe class NativeCleanup
{
    /// <summary>
    /// Invokes a non-throwing native cleanup function for one native address.
    /// </summary>
    /// <param name="cleanup">The non-null cdecl cleanup function address.</param>
    /// <param name="value">The native object address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Invoke(in nint cleanup, in nint value)
    {
        ((delegate* unmanaged[Cdecl]<nint, void>)cleanup)(value);
    }
}