// -----------------------------------------------------------------------
// <copyright file="BindingCompositeResultKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Identifies the managed representation of a composite result.
/// </summary>
public enum BindingCompositeResultKind
{
    /// <summary>
    /// An immutable positional record structure.
    /// </summary>
    RecordStruct = 0,

    /// <summary>
    /// An immutable sealed reference type.
    /// </summary>
    SealedClass = 1,
}