// -----------------------------------------------------------------------
// <copyright file="PipelineBuilderExtension.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.Occt.Generator.Services.Rules;

namespace TedToolkit.Occt.Generator;

/// <summary>
/// Registers the OCCT generator services and modules on a pipeline builder.
/// </summary>
public static class PipelineBuilderExtension
{
    /// <summary>
    /// Adds the OCCT generator services and modules to the pipeline builder.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">The generation options.</param>
    /// <returns>The configured pipeline builder.</returns>
    public static PipelineBuilder AddOcctGenerators(this PipelineBuilder builder, GenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services
            .AddSingleton<IVcpkgService, VcpkgService>()
            .AddSingleton<ITypeRule, DefaultTypeRule>()
            .AddSingleton<IResolver, Resolver>()
            .AddSingleton<IGenerationOutputCleaner, GenerationOutputCleaner>()
            .AddSingleton<IRecordModelManager, RecordModelManager>()
            .AddSingleton<IGeneratorService, GeneratorService>()
            .AddModule<CleanGenerationOutputModule>()
            .AddModule<ParseModule>()
            .AddModule<RecordLayoutModule>()
            .AddModule<GenerateModule>()
            .AddSingleton(
                Microsoft.Extensions.Options.Options.Create(options));

        return builder;
    }
}
