// -----------------------------------------------------------------------
// <copyright file="CgalProfileDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Describes one finite declaration requested by a CGAL profile.
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
    /// Gets the source token that independently demonstrates the declaration is reachable.
    /// </summary>
    public required string Evidence { get; init; }

    /// <summary>
    /// Gets a value indicating whether the declaration is an explicit profile root.
    /// </summary>
    public bool IsRoot { get; init; }

    /// <summary>
    /// Gets declaration identities required by this declaration.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}