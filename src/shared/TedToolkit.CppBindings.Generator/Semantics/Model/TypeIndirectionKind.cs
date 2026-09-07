// -----------------------------------------------------------------------
// <copyright file="TypeIndirectionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Identifies one native type indirection layer.
/// </summary>
public enum TypeIndirectionKind
{
    /// <summary>
    /// A pointer.
    /// </summary>
    PointerIndirection = 0,

    /// <summary>
    /// An lvalue reference.
    /// </summary>
    LValueReference = 1,

    /// <summary>
    /// An rvalue reference.
    /// </summary>
    RValueReference = 2,
}