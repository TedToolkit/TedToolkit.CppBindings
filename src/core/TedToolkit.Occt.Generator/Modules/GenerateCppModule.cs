// -----------------------------------------------------------------------
// <copyright file="GenerateCppModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Generates exactly one C++ invocation source for each parsed record.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
[DependsOn<ParseModule>]
public sealed class GenerateCppModule : Module<bool>
{
    private readonly IOptions<GenerationOptions> _generationOptions;

    private readonly IRecordModelManager _recordManager;

    private readonly IGeneratorService _generatorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCppModule"/> class.
    /// </summary>
    /// <param name="generationOptions">The generation options.</param>
    /// <param name="recordManager">The parsed record source.</param>
    /// <param name="generatorService">The language generator factory.</param>
    internal GenerateCppModule(
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
        var outputs = _recordManager.RecordModels
            .OrderBy(static record => record.Type.CSharpTypeName, StringComparer.Ordinal)
            .Select(static record => new RecordOutput(record, record.Type.CSharpTypeName + ".cpp"))
            .ToArray();
        RejectFileNameCollisions(outputs);

        _generationOptions.Value.CppFolder.Create();
        await Task.WhenAll(outputs.Select(output => GenerateRecordAsync(output, cancellationToken)))
            .ConfigureAwait(false);
        return true;
    }

    private static void RejectFileNameCollisions(IReadOnlyList<RecordOutput> outputs)
    {
        var paths = new Dictionary<string, RecordModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var output in outputs)
        {
            if (paths.TryAdd(output.FileName, output.Record))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Generated C++ source path collision '{output.FileName}' between records "
                + $"'{paths[output.FileName].Type.CppTypeName}' and '{output.Record.Type.CppTypeName}'.");
        }
    }

    private async Task GenerateRecordAsync(RecordOutput output, CancellationToken cancellationToken)
    {
        var source = await _generatorService.GenerateCpp(output.Record).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(_generationOptions.Value.CppFolder.FullName, output.FileName),
                source,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record RecordOutput(RecordModel Record, string FileName);
}