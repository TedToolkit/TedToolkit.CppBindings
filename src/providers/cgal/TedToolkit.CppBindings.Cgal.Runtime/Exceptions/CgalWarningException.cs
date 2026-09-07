// -----------------------------------------------------------------------
// <copyright file="CgalWarningException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal;

/// <summary>
/// Represents a native <c>CGAL::Warning_exception</c> when warnings are configured to throw.
/// </summary>
public sealed class CgalWarningException : CgalFailureException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CgalWarningException"/> class.
    /// </summary>
    /// <param name="message">The native message or deterministic Runtime fallback.</param>
    /// <param name="nativeTypeName">The copied native exception type name, if available.</param>
    /// <param name="nativeStackTrace">The copied native stack text, if available.</param>
    internal CgalWarningException(
        string message,
        string? nativeTypeName,
        string? nativeStackTrace)
        : base(message, nativeTypeName, nativeStackTrace)
    {
    }
}