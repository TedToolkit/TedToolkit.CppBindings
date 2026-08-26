// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Runtime.InteropServices;

using TedToolkit.Occt.Runtime;

namespace TedToolkit.Occt;

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
    /// <exception cref="IOcctException">The native error kind is nonzero.</exception>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        var errorKind = (NativeErrorKind)error.Kind;
        if (errorKind == NativeErrorKind.None)
        {
            return;
        }

        if ((nint)clear == 0)
        {
            throw new ArgumentNullException(nameof(clear));
        }

        string? nativeTypeName = null;
        string? message = null;
        string? nativeStackTrace = null;
        try
        {
            nativeTypeName = TryGetUtf8String(error.TypeName);
            message = TryGetUtf8String(error.Message);
            nativeStackTrace = TryGetUtf8String(error.StackTrace);
        }
        finally
        {
            fixed (NativeError* errorPointer = &error)
            {
                clear(errorPointer);
            }
        }

        throw CreateException(errorKind, message, nativeTypeName, nativeStackTrace);
    }

    private static Exception CreateException(
        NativeErrorKind errorKind,
        string? message,
        string? nativeTypeName,
        string? nativeStackTrace)
    {
        var exceptionMessage = string.IsNullOrEmpty(message)
            ? string.Format(
                CultureInfo.InvariantCulture,
                "Native OCCT operation failed with error kind {0}.",
                (int)errorKind)
            : message!;

        return errorKind switch
        {
            NativeErrorKind.Argument =>
                new OcctArgumentException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.ArgumentOutOfRange =>
                new OcctArgumentOutOfRangeException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.Arithmetic =>
                new OcctArithmeticException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.InvalidOperation =>
                new OcctInvalidOperationException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.NullObject =>
                new OcctNullObjectException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.OutOfMemory =>
                new OcctOutOfMemoryException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.Overflow =>
                new OcctOverflowException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.OcctFailure =>
                new OcctFailureException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.StandardException =>
                new OcctStandardException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.Unknown =>
                new OcctUnknownException(exceptionMessage, nativeTypeName, nativeStackTrace),
            _ => new OcctUnknownException(exceptionMessage, nativeTypeName, nativeStackTrace),
        };
    }

    private enum NativeErrorKind
    {
        None = 0,

        Argument = 1,

        ArgumentOutOfRange = 2,

        Arithmetic = 3,

        InvalidOperation = 4,

        NullObject = 5,

        OutOfMemory = 6,

        Overflow = 7,

        OcctFailure = 8,

        StandardException = 9,

        Unknown = 255,
    }

    private static string? TryGetUtf8String(in nint pointer)
    {
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(pointer);
        }
        catch (OutOfMemoryException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}