// -----------------------------------------------------------------------
// <copyright file="CppBindingsPipelineBuilderExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;
using ModularPipelines.Extensions;
using ModularPipelines.Modules;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Registers provider-neutral source generation using one provider and one completed plan per run.
/// </summary>
public static class CppBindingsPipelineBuilderExtensions
{
    /// <summary>
    /// Registers the clean and language publication stages after the provider's preparation modules.
    /// </summary>
    /// <param name="builder">The pipeline builder, with the provider's preparation modules registered.</param>
    /// <param name="options">Dedicated output roots and the supported target configuration.</param>
    /// <param name="provider">The provider supplying the completed source plan.</param>
    /// <returns>The same configured builder.</returns>
    /// <exception cref="ArgumentException">A preparation dependency is not a concrete module type.</exception>
    public static PipelineBuilder AddCppGenerators(
        this PipelineBuilder builder,
        GenerationOptions options,
        IGenerationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(provider.PreparationModules);
        GenerationOutput.ValidateOptions(options);
        if (provider.PreparationModules.Any(static type =>
                type?.IsAbstract != false || type.ContainsGenericParameters || !typeof(IModule).IsAssignableFrom(type)))
        {
            throw new ArgumentException("Preparation dependencies must be concrete pipeline module types.", nameof(provider));
        }

        var session = new GenerationSession(options, provider);
        builder.Services
            .AddModule<CleanGenerationOutputModule>(_ => new CleanGenerationOutputModule(session))
            .AddModule<GenerateCSharpModule>(_ => new GenerateCSharpModule(session))
            .AddModule<GenerateCppModule>(_ => new GenerateCppModule(session));
        return builder;
    }
}