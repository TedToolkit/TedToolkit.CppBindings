// -----------------------------------------------------------------------
// <copyright file="GenerateCSharpModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Publishes managed sources from the same completed plan as native generation.
/// </summary>
[DependsOn<CleanGenerationOutputModule>]
public sealed class GenerateCSharpModule : Module<bool>
{
    private readonly GenerationSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCSharpModule"/> class.
    /// </summary>
    /// <param name="session">The shared run state.</param>
    internal GenerateCSharpModule(GenerationSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var plan = await _session.GetPlanAsync(cancellationToken).ConfigureAwait(false);
        await GenerationOutput.PublishAsync(_session.Options.CSharpFolder, plan.CSharpSources, cancellationToken).ConfigureAwait(false);
        return true;
    }
}