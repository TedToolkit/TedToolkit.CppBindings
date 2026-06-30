// -----------------------------------------------------------------------
// <copyright file="CleanGenerationOutputModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Clears stale generator output before any parsing or generation work begins.
/// </summary>
public sealed class CleanGenerationOutputModule : Module<bool>
{
    private readonly IGenerationOutputCleaner _generationOutputCleaner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanGenerationOutputModule"/> class.
    /// </summary>
    /// <param name="generationOutputCleaner">The output cleaner.</param>
    internal CleanGenerationOutputModule(IGenerationOutputCleaner generationOutputCleaner)
    {
        _generationOutputCleaner = generationOutputCleaner;
    }

    /// <inheritdoc />
    protected override Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        _generationOutputCleaner.Clean();
        return Task.FromResult(true);
    }
}