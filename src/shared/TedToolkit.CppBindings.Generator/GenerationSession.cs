// -----------------------------------------------------------------------
// <copyright file="GenerationSession.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Generators;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Shares one finalized and validated provider plan across the registered generation stages.
/// </summary>
/// <param name="options">The validated shared configuration.</param>
/// <param name="provider">The provider for this pipeline run.</param>
internal sealed class GenerationSession(GenerationOptions options, IGenerationProvider provider)
{
    private readonly object _gate = new();

    private Task<GenerationPlan>? _plan;

    /// <summary>
    /// Gets the shared options for this run.
    /// </summary>
    internal GenerationOptions Options { get; } = options;

    /// <summary>
    /// Gets the snapshotted preparation module dependencies.
    /// </summary>
    internal IReadOnlyList<Type> PreparationModules { get; } = Array.AsReadOnly(provider.PreparationModules.ToArray());

    /// <summary>
    /// Gets the one shared plan task, preserving cancellation and preparation failure.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for this stage.</param>
    /// <returns>The finalized validated plan.</returns>
    internal Task<GenerationPlan> GetPlanAsync(in CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return (_plan ??= PrepareAsync(cancellationToken)).WaitAsync(cancellationToken);
        }
    }

    private async Task<GenerationPlan> PrepareAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerPlan = await provider.CreatePlanAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(providerPlan);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = new GenerationPlan(
            providerPlan.CSharpSources.Append(new GeneratedSource(
                NativeApiGenerator.FileName,
                (writer, token) => writer.WriteAsync(NativeApiGenerator.Generate(Options).AsMemory(), token))),
            providerPlan.CppSources.Append(new GeneratedSource(
                NativeFunctionTableGenerator.FileName,
                (writer, token) => writer.WriteAsync(NativeFunctionTableGenerator.Generate(providerPlan.NativeExports).AsMemory(), token))),
            providerPlan.NativeExports);
        GenerationOutput.ValidateSources(Options.CSharpFolder, plan.CSharpSources);
        GenerationOutput.ValidateSources(Options.CppFolder, plan.CppSources);
        GenerationOutput.ValidateExports(plan.NativeExports);
        return plan;
    }
}