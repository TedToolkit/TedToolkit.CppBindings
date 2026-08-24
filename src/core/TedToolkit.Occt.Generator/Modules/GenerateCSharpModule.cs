// -----------------------------------------------------------------------
// <copyright file="GenerateCSharpModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Generates the C++ and C# source files for each parsed record.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
[DependsOn<ParseModule>]
public sealed class GenerateCSharpModule : Module<bool>
{
    private readonly IOptions<GenerationOptions> _generationOptions;

    private readonly IRecordModelManager _recordManager;

    private readonly IGeneratorService _generatorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCSharpModule"/> class.
    /// </summary>
    /// <param name="generationOptions">The generation options.</param>
    /// <param name="recordManager">The record queue manager.</param>
    /// <param name="generatorService">The generator service.</param>
    internal GenerateCSharpModule(
        IOptions<GenerationOptions> generationOptions,
        IRecordModelManager recordManager,
        IGeneratorService generatorService)
    {
        _generationOptions = generationOptions;
        _recordManager = recordManager;
        _generatorService = generatorService;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tasks = new List<Task>();

        foreach (var recordManagerRecordModel in _recordManager.RecordModels)
        {
            tasks.Add(context.SubModule(
                recordManagerRecordModel.Type.CppTypeName,
                () => Task.WhenAll(
                    GenerateCSharpAsync(recordManagerRecordModel, cancellationToken))));
        }

        foreach (var enumModel in _recordManager.EnumModels)
        {
            tasks.Add(context.SubModule(
                enumModel.SourceType,
                () => GenerateCSharpAsync(enumModel, cancellationToken)));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return true;
    }

    private async Task GenerateCSharpAsync(RecordModel record, CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(_generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(record.Type.CSharpTypeName, ".g.cs"));

        var codes = await _generatorService.GenerateCSharp(record).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(csharpFile, codes, cancellationToken).ConfigureAwait(false);
    }

    private async Task GenerateCSharpAsync(EnumModel enumModel, CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(_generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(enumModel.Name, ".g.cs"));

        var codes = await _generatorService.GenerateCSharp(enumModel).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(csharpFile, codes, cancellationToken).ConfigureAwait(false);
    }
}