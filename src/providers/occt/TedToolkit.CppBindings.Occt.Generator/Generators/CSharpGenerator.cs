// -----------------------------------------------------------------------
// <copyright file="CSharpGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Services;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Adapts legacy provider tests to the Shared managed declaration emitter.
/// </summary>
/// <param name="record">The normalized record to emit.</param>
/// <param name="options">The provider generation options.</param>
/// <param name="recordCatalog">The available normalized records.</param>
/// <param name="nativeFunctionIndices">The exact function-table slot map.</param>
/// <param name="generateRepresentation">Whether to emit the managed representation.</param>
internal sealed class CSharpGenerator(
    RecordModel record,
    IOptions<OcctGenerationOptions> options,
    IReadOnlyDictionary<string, RecordModel>? recordCatalog = null,
    IReadOnlyDictionary<string, int>? nativeFunctionIndices = null,
    bool generateRepresentation = true) : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var profile = OcctSemanticProfile.CreateEmissionProfile(
            options.Value.CSharpNamespace,
            options.Value.IsInternal);
        return new BindingManagedEmitter(
            record,
            profile,
            recordCatalog,
            nativeFunctionIndices,
            generateRepresentation).GenerateAsync(cancellationToken);
    }
}