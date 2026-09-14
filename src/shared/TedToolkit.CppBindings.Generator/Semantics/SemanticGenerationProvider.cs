// -----------------------------------------------------------------------
// <copyright file="SemanticGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Bases a generation provider on the shared semantic engine without prescribing provider policy.
/// </summary>
public abstract class SemanticGenerationProvider : IGenerationProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticGenerationProvider"/> class.
    /// </summary>
    /// <param name="semanticEngine">The shared engine configured with provider-owned policy.</param>
    protected SemanticGenerationProvider(BindingSemanticEngine semanticEngine)
    {
        ArgumentNullException.ThrowIfNull(semanticEngine);
        SemanticEngine = semanticEngine;
    }

    /// <summary>
    /// Gets the shared semantic engine configured for this provider.
    /// </summary>
    protected BindingSemanticEngine SemanticEngine { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<Type> PreparationModules { get; }

    /// <inheritdoc />
    public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CreatePlanCoreAsync(cancellationToken);
    }

    /// <summary>
    /// Creates provider roots, renderers, and native metadata for Shared normalization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for plan creation.</param>
    /// <returns>The provider model input; Shared constructs the completed generation plan.</returns>
    protected abstract Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken);

    private async Task<GenerationPlan> CreatePlanCoreAsync(CancellationToken cancellationToken)
    {
        var providerModel = await CreateProviderModelAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(providerModel);
        cancellationToken.ThrowIfCancellationRequested();
        return SemanticEngine.CreatePlan(providerModel);
    }
}