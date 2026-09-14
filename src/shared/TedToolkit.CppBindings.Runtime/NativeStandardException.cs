// -----------------------------------------------------------------------
// <copyright file="NativeStandardException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents an otherwise-unclassified standard C++ exception.
/// </summary>
public sealed class NativeStandardException : NativeException
{
    /// <summary>
    /// Initializes a new instance from copied native diagnostics.
    /// </summary>
    /// <param name="message">The copied native message or deterministic fallback.</param>
    /// <param name="type">The copied native exception type name.</param>
    /// <param name="stack">The copied native stack text.</param>
    internal NativeStandardException(string message, string? type, string? stack)
        : base(message, type, stack)
    {
    }
}