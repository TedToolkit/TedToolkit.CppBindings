// -----------------------------------------------------------------------
// <copyright file="ITypeRule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Represents one rule that can project a clang type into a custom generator model.
/// </summary>
public interface ITypeRule
{
    /// <summary>
    /// Tries to project a type with this rule.
    /// </summary>
    /// <param name="type">The rule context.</param>
    /// <param name="recordModel">The projected model when the rule matches.</param>
    /// <returns><see langword="true"/> when the rule matched; otherwise, <see langword="false"/>.</returns>
    bool TryResolve(ClangSharp.Type type, out TypeModel recordModel, out CXXRecordDecl? decl);
}