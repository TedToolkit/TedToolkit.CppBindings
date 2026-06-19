// -----------------------------------------------------------------------
// <copyright file="GenerateModule.cs" company="TedToolkit">
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
/// <param name="generationOptions">The generation options.</param>
/// <param name="recordManager">The record queue manager.</param>
/// <param name="generatorService">The generator service.</param>
[DependsOn<CleanGenerationOutputModule>]
[DependsOn<ParseModule>]
public sealed class GenerateCppModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordModelManager recordManager,
    IGeneratorService generatorService,
    IVcpkgService vcpkgService) :
    Module<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compile = new CppCompileCoontext(generationOptions.Value.CppFolder, "ted_toolkit_occt",
            await vcpkgService.GetOcctCppVersionAsync().ConfigureAwait(false));

        var tasks = new List<Task> { CopyCppInteropHeaderAsync(compile, cancellationToken), };

        foreach (var recordManagerRecordModel in recordManager.RecordModels)
        {
            tasks.Add(context.SubModule(
                recordManagerRecordModel.Type.CppTypeName,
                () => Task.WhenAll(
                    GenerateCppAsync(compile, recordManagerRecordModel, cancellationToken))));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // var folder = await compile.BuildAsync(context.Shell, false, vcpkgService.GetRoot(), vcpkgService.GetTriplet(),
        //         cancellationToken)
        //     .ConfigureAwait(false);
        return true;
    }

    private async Task GenerateCppAsync(CppCompileCoontext compile, RecordModel record,
        CancellationToken cancellationToken)
    {
        var codes = await generatorService.GenerateCpp(record).GenerateAsync(cancellationToken).ConfigureAwait(false);
        await compile
            .AddSourceAsync(ZString.Concat(record.Type.CSharpPublicType.ToCode(), ".cpp"), codes, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task CopyCppInteropHeaderAsync(CppCompileCoontext compile, CancellationToken cancellationToken)
    {
        var cppFolder = generationOptions.Value.CppFolder;
        cppFolder.Create();

        var headerStream = typeof(GenerateCSharpModule).Assembly
            .GetManifestResourceStream("TedToolkit.Occt.Generator.Assets.cpp.csharp_interop.h");

        ArgumentNullException.ThrowIfNull(headerStream);

        using (var reader = new StreamReader(headerStream))
        {
            await compile.AddSourceAsync("csharp_interop.h",
                    await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}