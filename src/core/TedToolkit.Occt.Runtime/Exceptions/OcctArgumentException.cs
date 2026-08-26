// -----------------------------------------------------------------------
// <copyright file="OcctArgumentException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents an invalid argument reported by an executed native OCCT operation.
/// </summary>
public sealed class OcctArgumentException : ArgumentException, IOcctException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctArgumentException"/> class from copied native diagnostics.
    /// </summary>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal OcctArgumentException(
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(message, (string?)null)
    {
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <inheritdoc/>
    public string? NativeTypeName { get; }

    /// <inheritdoc/>
    public string? NativeStackTrace { get; }
}