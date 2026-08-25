// -----------------------------------------------------------------------
// <copyright file="IResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Models.Types;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Resolves Clang types into projected generator models.
/// </summary>
internal interface IResolver
{
    /// <summary>
    /// Resolves a Clang type into generator metadata.
    /// </summary>
    /// <param name="type">The Clang type to resolve.</param>
    /// <returns>The resolved projection.</returns>
    TypeResolveResult Resolve(ClangSharp.Type type);
}