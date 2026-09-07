// -----------------------------------------------------------------------
// <copyright file="EnumGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Adapts legacy provider tests to the Shared enum emitter.
/// </summary>
/// <param name="model">The normalized enum to emit.</param>
/// <param name="cSharpNamespace">The generated namespace.</param>
internal sealed class EnumGenerator(
    EnumModel model,
    string cSharpNamespace = "TedToolkit.CppBindings.Occt") : IGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        return new BindingEnumEmitter(model, cSharpNamespace)
            .GenerateAsync(cancellationToken);
    }
}