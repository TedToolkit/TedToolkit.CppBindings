// -----------------------------------------------------------------------
// <copyright file="GeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Creates generator instances for record declarations.
/// </summary>
/// <param name="generationOptions">The generation options.</param>
internal sealed class GeneratorService(
    IOptions<OcctGenerationOptions> generationOptions) : IGeneratorService
{
    /// <inheritdoc/>
    public IGenerator GenerateCpp(RecordModel record)
    {
        return new CppGenerator(record);
    }

    /// <inheritdoc/>
    public IGenerator GenerateCSharp(
        RecordModel record,
        IReadOnlyDictionary<string, RecordModel>? recordCatalog = null,
        IReadOnlyDictionary<string, int>? nativeFunctionIndices = null,
        bool generateRepresentation = true)
    {
        return new CSharpGenerator(
            record,
            generationOptions,
            recordCatalog,
            nativeFunctionIndices,
            generateRepresentation);
    }

    /// <inheritdoc/>
    public IGenerator GenerateCSharp(EnumModel enumModel)
    {
        return new EnumGenerator(enumModel, generationOptions.Value.CSharpNamespace);
    }
}