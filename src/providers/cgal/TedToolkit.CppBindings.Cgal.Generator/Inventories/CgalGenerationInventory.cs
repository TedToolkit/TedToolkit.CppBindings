// -----------------------------------------------------------------------
// <copyright file="CgalGenerationInventory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Holds deterministic source, declaration, and generated-artifact inventories for one resolved profile.
/// </summary>
public sealed record CgalGenerationInventory
{
    /// <summary>
    /// Gets every installed public CGAL header with a stable source disposition.
    /// </summary>
    public required IReadOnlyList<CgalSourceDisposition> Sources { get; init; }

    /// <summary>
    /// Gets every public declaration Clang observed in the selected header closure.
    /// </summary>
    public required IReadOnlyList<CgalDeclarationDisposition> SourceDeclarations { get; init; }

    /// <summary>
    /// Gets the finite candidate declarations.
    /// </summary>
    public required IReadOnlyList<CgalDeclarationDisposition> Candidates { get; init; }

    /// <summary>
    /// Gets the admitted candidate partition.
    /// </summary>
    public required IReadOnlyList<CgalDeclarationDisposition> Admitted { get; init; }

    /// <summary>
    /// Gets the unsupported candidate partition.
    /// </summary>
    public required IReadOnlyList<CgalDeclarationDisposition> Unsupported { get; init; }

    /// <summary>
    /// Gets the complete managed output inventory, including Shared support.
    /// </summary>
    public required IReadOnlyList<CgalArtifactInventoryEntry> ManagedArtifacts { get; init; }

    /// <summary>
    /// Gets the complete native output inventory, including Shared support.
    /// </summary>
    public required IReadOnlyList<CgalArtifactInventoryEntry> NativeArtifacts { get; init; }

    /// <summary>
    /// Gets the resolved native toolchain and package ABI identities.
    /// </summary>
    public required CgalResolvedToolchain Toolchain { get; init; }
}