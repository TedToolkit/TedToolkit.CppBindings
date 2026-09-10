// -----------------------------------------------------------------------
// <copyright file="OcctEmitterFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Services;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Configures the actual Shared emitters with the OCCT profile for focused tests.
/// </summary>
internal static class OcctEmitterFactory
{
    /// <summary>
    /// Creates the Shared managed emitter with the current OCCT profile.
    /// </summary>
    /// <param name="record">The normalized record to emit.</param>
    /// <param name="options">The provider generation options.</param>
    /// <param name="recordCatalog">The available normalized records.</param>
    /// <param name="nativeFunctionIndices">The exact function-table slot map.</param>
    /// <param name="generateRepresentation">Whether to emit the managed representation.</param>
    /// <returns>The configured Shared managed emitter.</returns>
    internal static BindingManagedEmitter Managed(
        RecordModel record,
        IOptions<OcctGenerationOptions> options,
        IReadOnlyDictionary<string, RecordModel>? recordCatalog = null,
        IReadOnlyDictionary<string, int>? nativeFunctionIndices = null,
        bool generateRepresentation = true)
    {
        var profile = OcctSemanticProfile.CreateEmissionProfile(
            options.Value.CSharpNamespace,
            options.Value.IsInternal);
        return new(record, profile, recordCatalog, nativeFunctionIndices, generateRepresentation);
    }

    /// <summary>
    /// Creates the Shared native emitter with the current OCCT profile.
    /// </summary>
    /// <param name="record">The normalized record to emit.</param>
    /// <returns>The configured Shared native emitter.</returns>
    internal static BindingNativeEmitter Native(RecordModel record)
    {
        return new(
            record,
            OcctSemanticProfile.CreateEmissionProfile("TedToolkit.CppBindings.Occt", isInternal: false));
    }

    /// <summary>
    /// Creates the Shared enum emitter with the requested managed namespace.
    /// </summary>
    /// <param name="model">The normalized enum to emit.</param>
    /// <param name="cSharpNamespace">The generated namespace.</param>
    /// <returns>The configured Shared enum emitter.</returns>
    internal static BindingEnumEmitter Enum(
        EnumModel model,
        string cSharpNamespace = "TedToolkit.CppBindings.Occt")
    {
        return new(model, cSharpNamespace);
    }
}