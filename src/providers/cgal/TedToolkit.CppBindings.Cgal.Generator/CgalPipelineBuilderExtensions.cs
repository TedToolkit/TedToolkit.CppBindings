// -----------------------------------------------------------------------
// <copyright file="CgalPipelineBuilderExtensions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines;

using TedToolkit.CppBindings.Generator;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Registers deterministic finite-profile CGAL generation on a Shared pipeline.
/// </summary>
public static class CgalPipelineBuilderExtensions
{
    /// <summary>
    /// Adds the CGAL provider and Shared publication modules.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">The CGAL generation options.</param>
    /// <returns>The configured builder.</returns>
    public static PipelineBuilder AddCgalGenerators(this PipelineBuilder builder, CgalGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.AddCppGenerators(options, new CgalGenerationProvider(options));
    }
}