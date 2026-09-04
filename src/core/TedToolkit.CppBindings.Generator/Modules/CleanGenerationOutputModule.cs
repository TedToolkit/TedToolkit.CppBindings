// -----------------------------------------------------------------------
// <copyright file="CleanGenerationOutputModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Validates the completed provider plan before reconciling dedicated generation output roots.
/// </summary>
public sealed class CleanGenerationOutputModule : Module<bool>
{
    private readonly GenerationSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanGenerationOutputModule"/> class.
    /// </summary>
    /// <param name="session">The shared run state.</param>
    internal CleanGenerationOutputModule(GenerationSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    protected override void DeclareDependencies(IDependencyDeclaration deps)
    {
        ArgumentNullException.ThrowIfNull(deps);
        foreach (var type in _session.PreparationModules)
        {
            _ = deps.DependsOn(type);
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var plan = await _session.GetPlanAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        GenerationOutput.Reconcile(_session.Options.CSharpFolder, plan.CSharpSources);
        GenerationOutput.Reconcile(_session.Options.CppFolder, plan.CppSources);
        return true;
    }
}