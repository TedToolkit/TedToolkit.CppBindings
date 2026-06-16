// -----------------------------------------------------------------------
// <copyright file="CppGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Produces the C++ translation unit payload for a parsed record.
/// </summary>
public sealed class CppGenerator : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult("");
    }
}