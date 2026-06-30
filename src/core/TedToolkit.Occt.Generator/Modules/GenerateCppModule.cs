// -----------------------------------------------------------------------
// <copyright file="GenerateCppModule.cs" company="TedToolkit">
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
public sealed class GenerateCppModule : Module<bool>
{
    private readonly IOptions<GenerationOptions> _generationOptions;

    private readonly IRecordModelManager _recordManager;

    private readonly IGeneratorService _generatorService;

    private readonly IVcpkgDefaultTripletResolver _defaultsResolver;

    private readonly IVcpkgEnvironment _vcpkgEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCppModule"/> class.
    /// </summary>
    /// <param name="generationOptions">The generation options.</param>
    /// <param name="recordManager">The record queue manager.</param>
    /// <param name="generatorService">The generator service.</param>
    /// <param name="defaultsResolver">The default triplet resolver.</param>
    /// <param name="vcpkgEnvironment">The vcpkg environment.</param>
    internal GenerateCppModule(
        IOptions<GenerationOptions> generationOptions,
        IRecordModelManager recordManager,
        IGeneratorService generatorService,
        IVcpkgDefaultTripletResolver defaultsResolver,
        IVcpkgEnvironment vcpkgEnvironment)
    {
        _generationOptions = generationOptions;
        _recordManager = recordManager;
        _generatorService = generatorService;
        _defaultsResolver = defaultsResolver;
        _vcpkgEnvironment = vcpkgEnvironment;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var triplet = _generationOptions.Value.GetTriplet(_defaultsResolver);

        var compile = new CppCompileCoontext(_generationOptions.Value.CppFolder, "ted_toolkit_occt",
            _generationOptions.Value.CppVersion);

        var tasks = new List<Task>()
        {
            GenerateHeadersAsync(compile, triplet, cancellationToken),
            CopyCppInteropHeaderAsync(compile, cancellationToken),
        };

        foreach (var recordManagerRecordModel in _recordManager.RecordModels)
        {
            tasks.Add(context.SubModule(
                recordManagerRecordModel.Type.CppTypeName,
                () => Task.WhenAll(
                    GenerateCppAsync(compile, recordManagerRecordModel, cancellationToken))));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var folder = await compile.BuildAsync(context.Shell, false, _vcpkgEnvironment.GetRoot(), triplet,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async Task GenerateCppAsync(CppCompileCoontext compile, RecordModel record,
        CancellationToken cancellationToken)
    {
        var codes = await _generatorService.GenerateCpp(record).GenerateAsync(cancellationToken).ConfigureAwait(false);
        await compile
            .AddSourceAsync(ZString.Concat(record.Type.CSharpTypeName, ".cpp"), codes, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task GenerateHeadersAsync(CppCompileCoontext compile, string triplet, CancellationToken cancellationToken)
    {
        var content = await _vcpkgEnvironment.GetIncludingHeaderContentAsync(triplet, cancellationToken).ConfigureAwait(false);
        await compile.AddSourceAsync("headers.h", content, cancellationToken).ConfigureAwait(false);
    }

    private async Task CopyCppInteropHeaderAsync(CppCompileCoontext compile, CancellationToken cancellationToken)
    {
        var cppFolder = _generationOptions.Value.CppFolder;
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