// -----------------------------------------------------------------------
// <copyright file="NativeOverflowException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents a native overflow failure.
/// </summary>
public sealed class NativeOverflowException : OverflowException, INativeException
{
    /// <summary>
    /// Initializes a new instance from copied native diagnostics.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="type">The copied native exception type name.</param>
    /// <param name="stack">The copied native stack text.</param>
    internal NativeOverflowException(string message, string? type, string? stack)
        : base(message)
    {
        NativeTypeName = type;
        NativeStackTrace = stack;
    }

    /// <inheritdoc/>
    public string? NativeTypeName { get; }

    /// <inheritdoc/>
    public string? NativeStackTrace { get; }
}