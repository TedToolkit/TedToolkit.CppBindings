// -----------------------------------------------------------------------
// <copyright file="GenerateCppModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Publishes native sources and the shared ordered function table after complete preparation.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
public sealed class GenerateCppModule : Module<bool>
{
    private readonly GenerationSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCppModule"/> class.
    /// </summary>
    /// <param name="session">The shared run state.</param>
    internal GenerateCppModule(GenerationSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var plan = await _session.GetPlanAsync(cancellationToken).ConfigureAwait(false);
        await GenerationOutput.PublishAsync(_session.Options.CppFolder, plan.CppSources, cancellationToken).ConfigureAwait(false);
        return true;
    }
}