// -----------------------------------------------------------------------
// <copyright file="NativeErrorProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.CppBindings.Manifold;

/// <summary>Copies and clears native diagnostics before mapping them to provider exceptions.</summary>
[GeneratedCodeOnly]
public static class NativeErrorProjection
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Returns on success or clears exactly once and throws the mapped failure.</summary>
    [GeneratedCodeOnly]
    public static unsafe void ThrowIfFailed(
        ref NativeError error,
        delegate* unmanaged[Cdecl]<NativeError*, void> clear)
    {
        if (error.Kind == 0)
        {
            return;
        }

        var kind = error.Kind;
        string? type = null;
        string? message = null;
        string? stack = null;
        try
        {
            type = Copy(error.TypeName);
            message = Copy(error.Message);
            stack = Copy(error.StackTrace);
        }
        finally
        {
            fixed (NativeError* pointer = &error)
            {
                clear(pointer);
            }
        }

        message ??= $"Native Manifold operation failed with error kind {kind}.";
        throw kind switch
        {
            1 => new ManifoldArgumentException(message, type, stack),
            2 => new ManifoldArgumentOutOfRangeException(message, type, stack),
            6 => new ManifoldOutOfMemoryException(message, type, stack),
            7 => new ManifoldOverflowException(message, type, stack),
            9 => new ManifoldException(message, type, stack),
            _ => new ManifoldUnknownException(message, type, stack),
        };
    }

    private static unsafe string? Copy(nint value)
    {
        if (value == 0)
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)value));
        }
        catch (Exception exception) when (exception is ArgumentException or OutOfMemoryException or OverflowException)
        {
            return null;
        }
    }
}
