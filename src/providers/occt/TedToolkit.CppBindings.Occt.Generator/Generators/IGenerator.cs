// -----------------------------------------------------------------------
// <copyright file="IGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Preserves the provider's internal test seam over a Shared source emitter.
/// </summary>
internal interface IGenerator
{
    /// <summary>
    /// Generates source text asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated source text.</returns>
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}