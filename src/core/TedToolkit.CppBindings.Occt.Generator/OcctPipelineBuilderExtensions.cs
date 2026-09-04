// -----------------------------------------------------------------------
// <copyright file="OcctPipelineBuilderExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Occt.Generator.Services;

namespace TedToolkit.CppBindings.Occt.Generator;

/// <summary>
/// Registers the OCCT generator services and modules on a pipeline builder.
/// </summary>
public static class OcctPipelineBuilderExtensions
{
    /// <summary>
    /// Adds the OCCT generator services and modules to the pipeline builder.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">The generation options.</param>
    /// <returns>The configured pipeline builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="options"/>, or
    /// <see cref="GenerationOptions.NativeLibraryBaseName"/> is null.</exception>
    /// <exception cref="ArgumentException"><see cref="GenerationOptions.NativeLibraryBaseName"/> is not a
    /// portable native library basename.</exception>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The registered singleton factory transfers provider disposal to the pipeline service container; failed registration disposes it.")]
    public static PipelineBuilder AddOcctGenerators(this PipelineBuilder builder, OcctGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        var provider = new OcctGenerationProvider(options);
        try
        {
            _ = builder.AddCppGenerators(options, provider);
            builder.Services
                .AddSingleton(_ => provider)
                .AddModule<OcctParseModule>(sp => sp.GetRequiredService<OcctGenerationProvider>().ParseModule)
                .AddModule<OcctCompilerProbeModule>(sp => sp.GetRequiredService<OcctGenerationProvider>().ProbeModule);
            return builder;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }
}