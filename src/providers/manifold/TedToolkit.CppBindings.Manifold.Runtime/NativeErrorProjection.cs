// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Manifold;

/// <summary>Copies and clears native diagnostics before mapping them to provider exceptions.</summary>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    /// <summary>Returns on success or clears exactly once and throws the mapped failure.</summary>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        global::TedToolkit.CppBindings.NativeErrorProjection.ThrowIfFailed(ref error, clear, "Manifold");
    }
}