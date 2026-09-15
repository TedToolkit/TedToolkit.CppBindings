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
        return CreatePlanCoreAsync(null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GenerationPlan> CreatePlanAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return CreatePlanCoreAsync(options, cancellationToken);
    }

    /// <summary>
    /// Creates provider roots, renderers, and native metadata for Shared normalization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for plan creation.</param>
    /// <returns>The provider model input; Shared constructs the completed generation plan.</returns>
    protected abstract Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates provider roots, renderers, and native metadata using the shared generation options.
    /// </summary>
    /// <param name="options">The shared configuration for this plan.</param>
    /// <param name="cancellationToken">Cancellation for plan creation.</param>
    /// <returns>The provider model input; Shared constructs the completed generation plan.</returns>
    /// <exception cref="NotSupportedException">
    /// A native library version was requested but this provider does not support version selection.
    /// </exception>
    protected virtual Task<BindingProviderModel> CreateProviderModelAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.NativeLibraryVersion is not null)
        {
            throw new NotSupportedException(
                $"Provider '{GetType().FullName}' does not support exact native library version selection.");
        }

        return CreateProviderModelAsync(cancellationToken);
    }

    private async Task<GenerationPlan> CreatePlanCoreAsync(
        GenerationOptions? options,
        CancellationToken cancellationToken)
    {
        var providerModel = options is null
            ? await CreateProviderModelAsync(cancellationToken).ConfigureAwait(false)
            : await CreateProviderModelAsync(options, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(providerModel);
        cancellationToken.ThrowIfCancellationRequested();
        return SemanticEngine.CreatePlan(providerModel);
    }
}