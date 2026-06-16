// -----------------------------------------------------------------------
// <copyright file="FieldService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Provides field metadata and offset calculations.
/// </summary>
/// <param name="generationOption">The generation options.</param>
/// <param name="typeService">The type naming service.</param>
public sealed class FieldService(IOptions<GenerationOptions> generationOption, ITypeService typeService) : IFieldService
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

    /// <inheritdoc/>
    public ValueTask<long> GetOffsetAsync(FieldDecl field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var rawOffset = field.Handle.OffsetOfField;
        if (!generationOption.Value.GetFieldOffsetByRunning && rawOffset >= 0)
        {
            return ValueTask.FromResult(rawOffset / 8);
        }

        // TODO: Vcpkg running check for the offset? It needs the options.
        return ValueTask.FromResult(field.Handle.OffsetOfField / 8);
    }
}