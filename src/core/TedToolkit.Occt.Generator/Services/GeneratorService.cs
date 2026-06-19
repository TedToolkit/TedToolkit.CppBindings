// -----------------------------------------------------------------------
// <copyright file="GeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Creates generator instances for record declarations.
/// </summary>
/// <param name="recordLayoutService">The native record layout service.</param>
/// <param name="generationOptions">The generation options.</param>
public sealed class GeneratorService(
    IOptions<GenerationOptions> generationOptions) : IGeneratorService
{
    /// <inheritdoc/>
    public CSharpGenerator GenerateCSharp(RecordModel record)
    {
        return new(record, generationOptions);
    }

    /// <inheritdoc/>
    public CppGenerator GenerateCpp(RecordModel record)
    {
        return new(record);
    }

    /// <inheritdoc/>
    public EnumGenerator GenerateCSharp(EnumModel enumModel)
    {
        return new(enumModel);
    }
}
