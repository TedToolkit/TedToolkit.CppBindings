// -----------------------------------------------------------------------
// <copyright file="OcctNullObjectException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents a null native OCCT object reported by an executed operation.
/// </summary>
public sealed class OcctNullObjectException : OcctException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctNullObjectException"/> class from copied native diagnostics.
    /// </summary>
    /// <param name="errorKind">The exact native error category.</param>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal OcctNullObjectException(
        OcctErrorKind errorKind,
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(errorKind, message, nativeTypeName, nativeStackTrace)
    {
    }
}