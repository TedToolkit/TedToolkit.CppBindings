// -----------------------------------------------------------------------
// <copyright file="CgalProfileManifest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Describes one versioned finite CGAL profile.
/// </summary>
public sealed record CgalProfileManifest
{
    /// <summary>
    /// Gets the profile schema version.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the stable profile identifier.
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// Gets the locked CGAL version.
    /// </summary>
    public required string CgalVersion { get; init; }

    /// <summary>
    /// Gets the locked vcpkg builtin baseline.
    /// </summary>
    public required string VcpkgBuiltinBaseline { get; init; }

    /// <summary>
    /// Gets the locked vcpkg triplet.
    /// </summary>
    public required string Triplet { get; init; }

    /// <summary>
    /// Gets the native kernel alias used by generated sources.
    /// </summary>
    public required string KernelAlias { get; init; }

    /// <summary>
    /// Gets the selected public headers that define the finite surface.
    /// </summary>
    public required IReadOnlyList<string> SelectedHeaders { get; init; }

    /// <summary>
    /// Gets every declaration discovered from the maintained finite surface.
    /// </summary>
    public required IReadOnlyList<CgalProfileDeclaration> Declarations { get; init; }

    /// <summary>
    /// Gets the locked toolchain identity.
    /// </summary>
    public required CgalToolchainIdentity Toolchain { get; init; }

    /// <summary>
    /// Loads the embedded default profile.
    /// </summary>
    /// <returns>The detached immutable profile.</returns>
    public static CgalProfileManifest LoadDefault()
    {
        return CgalProfileResources.LoadManifest();
    }
}