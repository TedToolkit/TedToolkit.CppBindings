// -----------------------------------------------------------------------
// <copyright file="TypeResolveResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Occt.Generator.Models.Types;

/// <summary>
/// Contains the resolved record, enum, and projected type for a Clang type.
/// </summary>
internal sealed class TypeResolveResult
{
    /// <summary>
    /// Gets the resolved record declaration, when the type maps to a record.
    /// </summary>
    public CXXRecordDecl? Decl { get; init; }

    /// <summary>
    /// Gets the resolved enum declaration, when the type maps to an enum.
    /// </summary>
    public EnumDecl? Enum { get; init; }

    /// <summary>
    /// Gets the projected type metadata.
    /// </summary>
    public required TypeModel Type { get; init; }
}