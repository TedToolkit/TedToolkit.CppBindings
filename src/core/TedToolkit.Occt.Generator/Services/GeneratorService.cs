// -----------------------------------------------------------------------
// <copyright file="GeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Creates generator instances for record declarations.
/// </summary>
/// <param name="generationOptions">The generation options.</param>
internal sealed class GeneratorService(
    IOptions<GenerationOptions> generationOptions) : IGeneratorService
{
    /// <inheritdoc/>
    public IGenerator GenerateCpp(RecordModel record)
    {
        return new CppGenerator(record);
    }

    /// <inheritdoc/>
    public IGenerator GenerateCSharp(RecordModel record)
    {
        return new CSharpGenerator(record, generationOptions);
    }

    /// <inheritdoc/>
    public IGenerator GenerateCSharp(EnumModel enumModel)
    {
        return new EnumGenerator(enumModel);
    }
}