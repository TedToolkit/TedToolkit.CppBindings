// -----------------------------------------------------------------------
// <copyright file="IDeclService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides common metadata access for declaration types.
/// </summary>
/// <typeparam name="TDecl">The declaration type.</typeparam>
public interface IDeclService<in TDecl>
    where TDecl : Decl
{
    /// <summary>
    /// Gets the declaration name.
    /// </summary>
    /// <param name="decl">The declaration.</param>
    /// <returns>The declaration name.</returns>
    string GetName(TDecl decl);

    /// <summary>
    /// Gets the declaration type.
    /// </summary>
    /// <param name="decl">The declaration.</param>
    /// <returns>The declaration type.</returns>
    ClangSharp.Type GetType(TDecl decl);
}