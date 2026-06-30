// -----------------------------------------------------------------------
// <copyright file="IGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Generates source text for a given record declaration.
/// </summary>
internal interface IGenerator
{
    /// <summary>
    /// Generates the source text asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated source text.</returns>
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}