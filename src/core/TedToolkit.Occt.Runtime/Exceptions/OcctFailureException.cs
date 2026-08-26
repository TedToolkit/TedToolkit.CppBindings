// -----------------------------------------------------------------------
// <copyright file="OcctFailureException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents a native OCCT <c>Standard_Failure</c> without a more specific managed exception type.
/// </summary>
public sealed class OcctFailureException : OcctException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctFailureException"/> class from copied native diagnostics.
    /// </summary>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal OcctFailureException(
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(message, nativeTypeName, nativeStackTrace)
    {
    }
}