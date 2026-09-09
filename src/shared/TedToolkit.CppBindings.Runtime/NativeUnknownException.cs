// -----------------------------------------------------------------------
// <copyright file="NativeUnknownException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents an unknown native thrown value or discriminator.
/// </summary>
public sealed class NativeUnknownException : NativeException
{
    /// <summary>
    /// Initializes a new instance from copied native diagnostics.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="type">The copied native exception type name.</param>
    /// <param name="stack">The copied native stack text.</param>
    internal NativeUnknownException(string message, string? type, string? stack)
        : base(message, type, stack)
    {
    }

    /// <summary>
    /// Initializes a new instance after a Provider extension failed to project the native error.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="type">The copied native exception type name.</param>
    /// <param name="stack">The copied native stack text.</param>
    /// <param name="innerException">The Provider projection failure.</param>
    internal NativeUnknownException(string message, string? type, string? stack, Exception innerException)
        : base(message, type, stack, innerException)
    {
    }
}