// -----------------------------------------------------------------------
// <copyright file="FieldService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Provides field metadata.
/// </summary>
/// <param name="typeService">The type naming service.</param>
public sealed class FieldService(ITypeService typeService) : IFieldService
{
    /// <inheritdoc/>
    public string GetName(FieldDecl decl)
    {
        ArgumentNullException.ThrowIfNull(decl);

        return decl.Name;
    }

    /// <inheritdoc/>
    public ClangSharp.Type GetType(FieldDecl decl)
    {
        ArgumentNullException.ThrowIfNull(decl);

        return typeService.DesugarType(decl.Type);
    }
}