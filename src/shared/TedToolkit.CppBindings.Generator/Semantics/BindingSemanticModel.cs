// -----------------------------------------------------------------------
// <copyright file="BindingSemanticModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Holds Shared's immutable normalized declaration closure and deterministic native export inventory.
/// </summary>
public sealed class BindingSemanticModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingSemanticModel"/> class.
    /// </summary>
    /// <param name="provider">The immutable provider input.</param>
    /// <param name="declarations">The normalized dependency closure.</param>
    /// <param name="nativeExports">The deterministic native export inventory.</param>
    internal BindingSemanticModel(
        BindingProviderModel provider,
        IEnumerable<BindingSemanticDeclaration> declarations,
        IEnumerable<string> nativeExports)
    {
        Provider = provider;
        Declarations = Array.AsReadOnly(declarations.ToArray());
        NativeExports = Array.AsReadOnly(nativeExports.ToArray());
    }

    /// <summary>
    /// Gets the normalized declaration dependency closure.
    /// </summary>
    public IReadOnlyList<BindingSemanticDeclaration> Declarations { get; }

    /// <summary>
    /// Gets the deterministic native export inventory shared by both binding sides.
    /// </summary>
    public IReadOnlyList<string> NativeExports { get; }

    /// <summary>
    /// Gets the provider inputs required to construct the final source inventories.
    /// </summary>
    internal BindingProviderModel Provider { get; }
}