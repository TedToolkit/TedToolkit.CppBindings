// -----------------------------------------------------------------------
// <copyright file="INativeException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings;

/// <summary>
/// Exposes diagnostics copied from a native exception.
/// </summary>
public interface INativeException
{
    /// <summary>
    /// Gets the copied native exception type name, or <see langword="null"/> when unavailable.
    /// </summary>
    string? NativeTypeName { get; }

    /// <summary>
    /// Gets the copied native stack text, or <see langword="null"/> when unavailable.
    /// </summary>
    string? NativeStackTrace { get; }
}