// -----------------------------------------------------------------------
// <copyright file="OcctStandardException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt;

/// <summary>
/// Represents a standard C++ exception reported by a native OCCT operation.
/// </summary>
public sealed class OcctStandardException : OcctException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctStandardException"/> class from copied native diagnostics.
    /// </summary>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal OcctStandardException(
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(message, nativeTypeName, nativeStackTrace)
    {
    }
}