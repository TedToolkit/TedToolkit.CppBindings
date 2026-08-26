// -----------------------------------------------------------------------
// <copyright file="IOcctException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents the diagnostics associated with a projected native OCCT failure.
/// </summary>
/// <remarks>
/// This interface identifies a diagnostic shape, not trusted native provenance. The library-provided
/// <c>Occt*Exception</c> types are constructed only by Runtime.
/// </remarks>
public interface IOcctException
{
    /// <summary>
    /// Gets the exact native error category, including an undefined reserved value when reported.
    /// </summary>
    OcctErrorKind ErrorKind { get; }

    /// <summary>
    /// Gets the copied native exception type name, or <see langword="null"/> when unavailable.
    /// </summary>
    string? NativeTypeName { get; }

    /// <summary>
    /// Gets the copied native stack text, or <see langword="null"/> when capture was unavailable.
    /// </summary>
    /// <remarks>
    /// This value is independent of <see cref="Exception.StackTrace"/>, which records the managed
    /// throw path.
    /// </remarks>
    string? NativeStackTrace { get; }
}