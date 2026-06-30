// -----------------------------------------------------------------------
// <copyright file="MethodModelType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Identifies the normalized kind of a parsed C++ method declaration.
/// </summary>
internal enum MethodModelType
{
    /// <summary>
    /// Represents a regular method call.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Represents a constructor entry point.
    /// </summary>
    New = 1,

    /// <summary>
    /// Represents a destructor entry point.
    /// </summary>
    Delete = 2,

    /// <summary>
    /// Represents an overloaded operator.
    /// </summary>
    Operator = 3,

    /// <summary>
    /// Represents an implicit conversion operator.
    /// </summary>
    Implicit = 4,

    /// <summary>
    /// Represents an explicit conversion operator.
    /// </summary>
    Explicit = 5,
}