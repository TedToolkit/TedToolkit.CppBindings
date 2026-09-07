// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.CppBindings.Cgal;

/// <summary>
/// Projects the generated native diagnostic owner into the fixed CGAL exception taxonomy.
/// </summary>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Returns on success or copies, clears exactly once, and throws a mapped CGAL exception.
    /// </summary>
    /// <param name="error">The authoritative native diagnostic owner slot.</param>
    /// <param name="clear">The non-throwing clear export from the native library that produced the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clear"/> is null for a failure.</exception>
    /// <exception cref="CgalException">The native error kind is nonzero.</exception>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        var kind = (NativeErrorKind)error.Kind;
        if (kind == NativeErrorKind.None)
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

        throw CreateException(kind, message, nativeTypeName, nativeStackTrace);
    }

    private static CgalException CreateException(
        NativeErrorKind kind,
        string? message,
        string? nativeTypeName,
        string? nativeStackTrace)
    {
        var exceptionMessage = string.IsNullOrEmpty(message)
            ? string.Format(CultureInfo.InvariantCulture, "Native CGAL operation failed with error kind {0}.", (int)kind)
            : message;
        return kind switch
        {
            NativeErrorKind.Argument => new CgalArgumentException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.ArgumentOutOfRange =>
                new CgalArgumentOutOfRangeException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.OutOfMemory =>
                new CgalOutOfMemoryException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.Arithmetic =>
                new CgalArithmeticException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.StandardException =>
                new CgalStandardException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalError => new CgalErrorException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalPrecondition =>
                new CgalPreconditionException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalPostcondition =>
                new CgalPostconditionException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalAssertion =>
                new CgalAssertionException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalTest => new CgalTestException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalWarning =>
                new CgalWarningException(exceptionMessage, nativeTypeName, nativeStackTrace),
            NativeErrorKind.CgalFailure =>
                new CgalFailureException(exceptionMessage, nativeTypeName, nativeStackTrace),
            _ => new CgalUnknownException(exceptionMessage, nativeTypeName, nativeStackTrace),
        };
    }

    private static unsafe string? TryGetUtf8String(in nint pointer)
    {
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)pointer));
        }
        catch (Exception exception) when (exception is ArgumentException or OutOfMemoryException or OverflowException)
        {
            return null;
        }
    }

    private enum NativeErrorKind
    {
        None = 0,

        Argument = 1,

        ArgumentOutOfRange = 2,

        OutOfMemory = 6,

        Arithmetic = 7,

        StandardException = 9,

        CgalError = 10,

        CgalPrecondition = 11,

        CgalPostcondition = 12,

        CgalAssertion = 13,

        CgalTest = 14,

        CgalWarning = 15,

        CgalFailure = 16,

        Unknown = 255,
    }
}