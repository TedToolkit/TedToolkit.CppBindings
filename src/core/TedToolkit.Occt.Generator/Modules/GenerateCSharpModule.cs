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

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Generates the C++ and C# source files for each parsed record.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
[DependsOn<CompilerProbeModule>]
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
        _generationOptions.Value.CSharpFolder.Create();
        var records = _recordManager.RecordModels.ToArray();
        var enums = _recordManager.EnumModels.ToArray();
        var recordCatalog = records.ToDictionary(
            static record => record.Type.CppTypeName,
            StringComparer.Ordinal);

        await Task.WhenAll(
                GenerateNativeApiAsync(cancellationToken),
                Parallel.ForEachAsync(
                    records,
                    cancellationToken,
                    (record, token) => new ValueTask(GenerateCSharpAsync(record, recordCatalog, token))),
                Parallel.ForEachAsync(
                    enums,
                    cancellationToken,
                    (enumModel, token) => new ValueTask(GenerateCSharpAsync(enumModel, token))))
            .ConfigureAwait(false);
        return true;
    }

    private Task GenerateNativeApiAsync(in CancellationToken cancellationToken)
    {
        var records = _recordManager.RecordModels.ToArray();
        var source = NativeApiGenerator.Generate(
            records,
            _generationOptions.Value.GetNativeLibraryBaseName());
        return File.WriteAllTextAsync(
            Path.Combine(_generationOptions.Value.CSharpFolder.FullName, NativeApiGenerator.FileName),
            source,
            cancellationToken);
    }

    private async Task GenerateCSharpAsync(
        RecordModel record,
        IReadOnlyDictionary<string, RecordModel> recordCatalog,
        CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(_generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(record.Type.CSharpTypeName.ToGeneratedFileStem(), ".g.cs"));

        var codes = await _generatorService.GenerateCSharp(record, recordCatalog).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(csharpFile, codes, cancellationToken).ConfigureAwait(false);
    }

    private async Task GenerateCSharpAsync(EnumModel enumModel, CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(_generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(enumModel.Name.ToGeneratedFileStem(), ".g.cs"));

        var codes = await _generatorService.GenerateCSharp(enumModel).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(csharpFile, codes, cancellationToken).ConfigureAwait(false);
    }
}