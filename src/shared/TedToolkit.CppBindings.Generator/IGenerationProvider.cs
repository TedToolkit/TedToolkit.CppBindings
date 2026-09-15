// -----------------------------------------------------------------------
// <copyright file="IGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Supplies a finalized generation plan after its registered preparation modules complete.
/// </summary>
public interface IGenerationProvider
{
    /// <summary>
    /// Gets the pipeline module types that must complete before plan creation or rendering.
    /// </summary>
    public IReadOnlyList<Type> PreparationModules { get; }

    /// <summary>
    /// Creates one finalized plan for this pipeline run without beginning source publication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for preparation and plan creation.</param>
    /// <returns>The completed plan; failures and cancellation propagate to the pipeline.</returns>
    public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates one finalized plan using the shared generation options.
    /// </summary>
    /// <param name="options">The shared configuration for the generation run.</param>
    /// <param name="cancellationToken">Cancellation for preparation and plan creation.</param>
    /// <returns>The completed plan; failures and cancellation propagate to the pipeline.</returns>
    /// <exception cref="NotSupportedException">
    /// A native library version was requested but this provider does not support version selection.
    /// </exception>
    public Task<GenerationPlan> CreatePlanAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.NativeLibraryVersion is not null)
        {
            throw new NotSupportedException(
                $"Provider '{GetType().FullName}' does not support exact native library version selection.");
        }

        return CreatePlanAsync(cancellationToken);
    }
}