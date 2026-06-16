// -----------------------------------------------------------------------
// <copyright file="GeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Creates generator instances for record declarations.
/// </summary>
/// <param name="recordService">The record metadata service.</param>
/// <param name="generationOptions">The generation options.</param>
/// <param name="typeService">The type naming service.</param>
/// <param name="fieldService">The field metadata service.</param>
public sealed class GeneratorService(
    IRecordService recordService,
    IOptions<GenerationOptions> generationOptions,
    ITypeService typeService,
    IFieldService fieldService) : IGeneratorService
{
    /// <inheritdoc/>
    public CSharpGenerator GenerateCSharp(CXXRecordDecl record)
    {
        return new(record, recordService, generationOptions, typeService, fieldService);
    }

    /// <inheritdoc/>
    public CppGenerator GenerateCpp(CXXRecordDecl record)
    {
        return new();
    }
}