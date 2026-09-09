// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Occt;

/// <summary>
/// Represents the generated-only projection boundary from native error transport to managed exceptions.
/// </summary>
/// <remarks>
/// Public visibility allows independently generated wrapper assemblies to invoke the shared
/// projection path. Handwritten callers should use generated OCCT operations instead.
/// </remarks>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    /// <summary>
    /// Returns for success or copies diagnostics, consumes the native owner, and throws the mapped failure.
    /// </summary>
    /// <param name="error">The authoritative native error owner slot.</param>
    /// <param name="clear">The non-throwing <c>cdecl</c> clear entry point from the library that produced the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clear"/> is <see langword="null"/> for a failure.</exception>
    /// <exception cref="INativeException">The native error kind is nonzero.</exception>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        global::TedToolkit.CppBindings.NativeErrorProjection.ThrowIfFailed(
            ref error,
            clear,
            "OCCT",
            CreateException);
    }

    private static Exception? CreateException(
        int errorKind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
    {
        return errorKind switch
        {
            9 => new OcctFailureException(message, nativeTypeName, nativeStackTrace),
            _ => null,
        };
    }
}