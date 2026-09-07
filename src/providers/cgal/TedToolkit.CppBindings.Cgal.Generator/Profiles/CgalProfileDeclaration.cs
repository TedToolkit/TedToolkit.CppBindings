// -----------------------------------------------------------------------
// <copyright file="CgalProfileDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Describes one finite candidate declaration and its admission proof.
/// </summary>
public sealed record CgalProfileDeclaration
{
    /// <summary>
    /// Gets the stable declaration identity.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the exact native spelling or closed signature.
    /// </summary>
    public required string NativeSignature { get; init; }

    /// <summary>
    /// Gets the source header relative to the vcpkg include root.
    /// </summary>
    public required string Header { get; init; }

    /// <summary>
    /// Gets the declaration category.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the stable disposition: admitted or unsupported.
    /// </summary>
    public required string Disposition { get; init; }

    /// <summary>
    /// Gets the narrow admission or rejection proof.
    /// </summary>
    public required string Proof { get; init; }
}