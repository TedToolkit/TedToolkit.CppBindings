// -----------------------------------------------------------------------
// <copyright file="CgalException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal;

/// <summary>
/// Represents the common base for every provider-defined CGAL failure or result-contract exception.
/// </summary>
public abstract class CgalException : Exception
{
    /// <summary>
    /// Initializes a new instance from copied native diagnostics.
    /// </summary>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    protected CgalException(string message, string? nativeTypeName, string? nativeStackTrace)
        : base(message)
    {
        NativeTypeName = nativeTypeName;
        NativeStackTrace = nativeStackTrace;
    }

    /// <summary>
    /// Gets the copied native exception type name, or <see langword="null"/> when unavailable.
    /// </summary>
    public string? NativeTypeName { get; }

    /// <summary>
    /// Gets the copied native stack text, or <see langword="null"/> when unavailable.
    /// </summary>
    /// <remarks>This is independent of <see cref="Exception.StackTrace"/>, which records the managed throw path.</remarks>
    public string? NativeStackTrace { get; }
}