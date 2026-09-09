// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.CppBindings;

/// <summary>
/// Projects the common native-error transport into Shared or Provider-specific managed exceptions.
/// </summary>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// Returns for success or copies diagnostics, clears their originating owner, and throws the mapped failure.
    /// </summary>
    /// <param name="error">The authoritative native diagnostic owner slot.</param>
    /// <param name="clear">The non-throwing clear export from the native module that produced the error.</param>
    /// <param name="providerName">The Provider name used only in a fallback message.</param>
    /// <param name="extension">The optional Provider-local exception factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clear"/> is null for a failure.</exception>
    /// <exception cref="INativeException">The native error kind is nonzero.</exception>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear,
        string? providerName = null,
        NativeErrorExtension? extension = null)
    {
        if (error.Kind == 0)
        {
            return;
        }

        if ((nint)clear == 0)
        {
            throw new ArgumentNullException(nameof(clear));
        }

        var kind = error.Kind;
        string? nativeTypeName;
        string? message;
        string? nativeStackTrace;
        try
        {
            nativeTypeName = CopyUtf8(error.TypeName);
            message = CopyUtf8(error.Message);
            nativeStackTrace = CopyUtf8(error.StackTrace);
        }
        finally
        {
            fixed (NativeError* errorPointer = &error)
            {
                clear(errorPointer);
            }
        }

        var exceptionMessage = string.IsNullOrEmpty(message)
            ? CreateFallbackMessage(kind, providerName)
            : message;
        throw CreateException(kind, exceptionMessage, nativeTypeName, nativeStackTrace, extension);
    }

    private static Exception CreateException(
        int kind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace,
        NativeErrorExtension? extension)
    {
        return kind switch
        {
            1 => new NativeArgumentException(message, nativeTypeName, nativeStackTrace),
            2 => new NativeArgumentOutOfRangeException(message, nativeTypeName, nativeStackTrace),
            3 => new NativeArithmeticException(message, nativeTypeName, nativeStackTrace),
            4 => new NativeInvalidOperationException(message, nativeTypeName, nativeStackTrace),
            5 => new NativeNullObjectException(message, nativeTypeName, nativeStackTrace),
            6 => new NativeOutOfMemoryException(message, nativeTypeName, nativeStackTrace),
            7 => new NativeOverflowException(message, nativeTypeName, nativeStackTrace),
            8 => new NativeStandardException(message, nativeTypeName, nativeStackTrace),
            255 => new NativeUnknownException(message, nativeTypeName, nativeStackTrace),
            _ => CreateProviderException(kind, message, nativeTypeName, nativeStackTrace, extension),
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A Provider projection failure must remain secondary to the transported native failure.")]
    private static Exception CreateProviderException(
        int kind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace,
        NativeErrorExtension? extension)
    {
        try
        {
            return extension?.Invoke(kind, message, nativeTypeName, nativeStackTrace)
                ?? new NativeUnknownException(message, nativeTypeName, nativeStackTrace);
        }
        catch (Exception exception)
        {
            return new NativeUnknownException(message, nativeTypeName, nativeStackTrace, exception);
        }
    }

    private static string CreateFallbackMessage(int kind, in string? providerName)
    {
        return string.IsNullOrWhiteSpace(providerName)
            ? string.Format(CultureInfo.InvariantCulture, "Native operation failed with error kind {0}.", kind)
            : string.Format(
                CultureInfo.InvariantCulture,
                "Native {0} operation failed with error kind {1}.",
                providerName,
                kind);
    }

    private static unsafe string? CopyUtf8(in nint pointer)
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
}