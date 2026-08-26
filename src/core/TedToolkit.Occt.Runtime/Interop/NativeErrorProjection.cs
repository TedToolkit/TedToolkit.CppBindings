// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.Occt;

/// <summary>
/// Projects the private native error carrier into the public managed exception contract.
/// </summary>
internal static class NativeErrorProjection
{
    /// <summary>
    /// Returns for success or copies diagnostics, consumes the native owner, and throws the mapped failure.
    /// </summary>
    /// <param name="error">The authoritative native error owner slot.</param>
    /// <param name="clear">The non-throwing clear entry point from the library that produced the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clear"/> is <see langword="null"/> for a failure.</exception>
    /// <exception cref="IOcctException">The native error kind is nonzero.</exception>
    internal static void ThrowIfFailed(ref NativeError error, NativeErrorClear clear)
    {
        var errorKind = (OcctErrorKind)error.Kind;
        if (errorKind == OcctErrorKind.None)
        {
            return;
        }

#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(clear);
#else
        if (clear is null)
        {
            throw new ArgumentNullException(nameof(clear));
        }
#endif

        string? nativeTypeName = null;
        string? message = null;
        string? nativeStackTrace = null;
        try
        {
            nativeTypeName = TryCopyUtf8(error.TypeName);
            message = TryCopyUtf8(error.Message);
            nativeStackTrace = TryCopyUtf8(error.StackTrace);
        }
        finally
        {
            clear(ref error);
        }

        throw CreateException(errorKind, message, nativeTypeName, nativeStackTrace);
    }

    private static Exception CreateException(
        OcctErrorKind errorKind,
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
            OcctErrorKind.Argument =>
                new OcctArgumentException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.ArgumentOutOfRange =>
                new OcctArgumentOutOfRangeException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.Arithmetic =>
                new OcctArithmeticException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.InvalidOperation =>
                new OcctInvalidOperationException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.NullObject =>
                new OcctNullObjectException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.OutOfMemory =>
                new OcctOutOfMemoryException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            OcctErrorKind.Overflow =>
                new OcctOverflowException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
            _ => new OcctException(errorKind, exceptionMessage, nativeTypeName, nativeStackTrace),
        };
    }

    private static string? TryCopyUtf8(
#if NET6_0_OR_GREATER || NETSTANDARD2_1
        in nint pointer)
#else
        nint pointer)
#endif
    {
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return CopyUtf8(pointer);
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

    private static unsafe string CopyUtf8(
#if NET6_0_OR_GREATER || NETSTANDARD2_1
        in nint pointer)
#else
        nint pointer)
#endif
    {
        var bytes = (byte*)pointer;
        var length = 0;
        while (bytes[length] != 0)
        {
            length = checked(length + 1);
        }

        if (length == 0)
        {
            return "";
        }

        var buffer = new byte[length];
        Marshal.Copy(pointer, buffer, 0, length);
        return Encoding.UTF8.GetString(buffer);
    }
}