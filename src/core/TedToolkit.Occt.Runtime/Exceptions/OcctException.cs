// -----------------------------------------------------------------------
// <copyright file="OcctException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents a native OCCT failure that has no more specific approved .NET exception base.
/// </summary>
public class OcctException : Exception, IOcctException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctException"/> class from copied native diagnostics.
    /// </summary>
    /// <param name="errorKind">The exact native error category.</param>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal OcctException(
        OcctErrorKind errorKind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(message)
    {
        ErrorKind = errorKind;
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <inheritdoc/>
    public OcctErrorKind ErrorKind { get; }

    /// <inheritdoc/>
    public string? NativeTypeName { get; }

    /// <inheritdoc/>
    public string? NativeStackTrace { get; }
}