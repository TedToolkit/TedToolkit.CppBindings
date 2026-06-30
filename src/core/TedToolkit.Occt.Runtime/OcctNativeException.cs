// -----------------------------------------------------------------------
// <copyright file="OcctNativeException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Represents an exception reported by the native OCCT interop layer.
/// </summary>
#pragma warning disable RCS1194, CA1032
public class OcctNativeException : ExternalException
#pragma warning restore CA1032, RCS1194
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctNativeException"/> class.
    /// </summary>
    /// <param name="nativeTypeName">The native exception type name.</param>
    /// <param name="message">The native exception message.</param>
    /// <param name="nativeStackTrace">The native stack trace captured from OCCT, if any.</param>
    internal OcctNativeException(string? nativeTypeName, string? message, string? nativeStackTrace = null)
        : base(message ?? "The native OCCT layer reported an unknown error.")
    {
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <summary>
    /// Gets the native exception type name.
    /// </summary>
    public string? NativeTypeName { get; }

    /// <summary>
    /// Gets the native stack trace captured from the OCCT side.
    /// </summary>
    public string? NativeStackTrace { get; }
}