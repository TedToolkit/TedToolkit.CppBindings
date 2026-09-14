// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings.Cgal;

/// <summary>
/// Projects the generated native diagnostic owner into the Shared taxonomy with CGAL extensions.
/// </summary>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    /// <summary>
    /// Returns on success or copies, clears exactly once, and throws a mapped native exception.
    /// </summary>
    /// <param name="error">The authoritative native diagnostic owner slot.</param>
    /// <param name="clear">The non-throwing clear export from the native library that produced the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clear"/> is null for a failure.</exception>
    /// <exception cref="INativeException">The native error kind is nonzero.</exception>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        global::TedToolkit.CppBindings.NativeErrorProjection.ThrowIfFailed(
            ref error,
            clear,
            "CGAL",
            CreateException);
    }

    private static Exception? CreateException(
        int kind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
    {
        return kind switch
        {
            10 => new CgalErrorException(message, nativeTypeName, nativeStackTrace),
            11 => new CgalPreconditionException(message, nativeTypeName, nativeStackTrace),
            12 => new CgalPostconditionException(message, nativeTypeName, nativeStackTrace),
            13 => new CgalAssertionException(message, nativeTypeName, nativeStackTrace),
            14 => new CgalTestException(message, nativeTypeName, nativeStackTrace),
            15 => new CgalWarningException(message, nativeTypeName, nativeStackTrace),
            16 => new CgalFailureException(message, nativeTypeName, nativeStackTrace),
            _ => null,
        };
    }
}