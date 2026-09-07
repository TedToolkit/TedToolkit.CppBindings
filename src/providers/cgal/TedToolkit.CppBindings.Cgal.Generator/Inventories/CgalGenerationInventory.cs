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
    /// Gets the finite candidate declarations.
    /// </summary>
    public required IReadOnlyList<CgalProfileDeclaration> Candidates { get; init; }

    /// <summary>
    /// Gets the admitted candidate partition.
    /// </summary>
    public required IReadOnlyList<CgalProfileDeclaration> Admitted { get; init; }

    /// <summary>
    /// Gets the unsupported candidate partition.
    /// </summary>
    public required IReadOnlyList<CgalProfileDeclaration> Unsupported { get; init; }

    /// <summary>
    /// Gets the complete managed output inventory, including Shared support.
    /// </summary>
    public required IReadOnlyList<string> ManagedFiles { get; init; }

    /// <summary>
    /// Gets the complete native output inventory, including Shared support.
    /// </summary>
    public required IReadOnlyList<string> NativeFiles { get; init; }
}