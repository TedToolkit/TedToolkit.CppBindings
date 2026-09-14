// -----------------------------------------------------------------------
// <copyright file="NativeException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents a native failure without a more appropriate .NET exception base.
/// </summary>
public abstract class NativeException : Exception, INativeException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeException"/> class.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name.</param>
    /// <param name="nativeStackTrace">The copied native stack text.</param>
    internal NativeException(string message, string? nativeTypeName, string? nativeStackTrace)
        : this(message, nativeTypeName, nativeStackTrace, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeException"/> class.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name.</param>
    /// <param name="nativeStackTrace">The copied native stack text.</param>
    /// <param name="innerException">The managed projection failure, when one occurred.</param>
    internal NativeException(
        string message,
        string? nativeTypeName,
        string? nativeStackTrace,
        Exception? innerException)
        : base(message, innerException)
    {
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <inheritdoc/>
    public string? NativeTypeName { get; }

    /// <inheritdoc/>
    public string? NativeStackTrace { get; }
}