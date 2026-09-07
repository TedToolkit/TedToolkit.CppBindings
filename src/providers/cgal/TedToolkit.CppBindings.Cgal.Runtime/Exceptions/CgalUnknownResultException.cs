// -----------------------------------------------------------------------
// <copyright file="CgalUnknownResultException.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal;

/// <summary>
/// Represents a nonempty polymorphic result alternative not declared by the finite profile.
/// </summary>
public sealed class CgalUnknownResultException : CgalException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CgalUnknownResultException"/> class.
    /// </summary>
    /// <param name="message">The generated operation-specific diagnostic.</param>
    public CgalUnknownResultException(string message)
        : base(message, null, null)
    {
    }
}