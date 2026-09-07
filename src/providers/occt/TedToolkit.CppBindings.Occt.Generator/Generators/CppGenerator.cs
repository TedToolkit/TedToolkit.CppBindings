// -----------------------------------------------------------------------
// <copyright file="CppGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Services;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Adapts legacy provider tests to the Shared native declaration emitter.
/// </summary>
/// <param name="record">The normalized record to emit.</param>
internal sealed class CppGenerator(RecordModel record) : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        return new BindingNativeEmitter(
            record,
            OcctSemanticProfile.CreateEmissionProfile("TedToolkit.CppBindings.Occt", isInternal: false))
            .GenerateAsync(cancellationToken);
    }
}