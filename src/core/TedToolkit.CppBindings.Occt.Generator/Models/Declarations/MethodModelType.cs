// -----------------------------------------------------------------------
// <copyright file="MethodModelType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

/// <summary>
/// Identifies the normalized kind of a parsed C++ method declaration.
/// </summary>
internal enum MethodModelType
{
    /// <summary>
    /// Represents a regular method call.
    /// </summary>
    NORMAL = 0,

    /// <summary>
    /// Represents a constructor entry point.
    /// </summary>
    NEW = 1,

    /// <summary>
    /// Represents a destructor entry point.
    /// </summary>
    DELETE = 2,

    /// <summary>
    /// Represents an overloaded operator.
    /// </summary>
    OPERATOR = 3,

    /// <summary>
    /// Represents an implicit conversion operator.
    /// </summary>
    IMPLICIT = 4,

    /// <summary>
    /// Represents an explicit conversion operator.
    /// </summary>
    EXPLICIT = 5,

    /// <summary>
    /// Represents direct destruction of an object returned with value ownership.
    /// </summary>
    VALUE_DELETE = 6,

    /// <summary>
    /// Represents release of one intrusive reference owned by an OCCT handle.
    /// </summary>
    HANDLE_RELEASE = 7,
}