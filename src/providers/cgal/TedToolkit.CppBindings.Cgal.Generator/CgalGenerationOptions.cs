// -----------------------------------------------------------------------
// <copyright file="CgalGenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Configures one finite CGAL generation run.
/// </summary>
public sealed record CgalGenerationOptions : GenerationOptions
{
    /// <summary>
    /// Gets the identifier of the default finite Windows EPICK profile.
    /// </summary>
    public const string DefaultProfileId = "epick-windows-v1";

    /// <summary>
    /// Gets the vcpkg root that supplies the locked CGAL installation.
    /// </summary>
    public required DirectoryInfo VcpkgRoot { get; init; }

    /// <summary>
    /// Gets the finite profile identifier.
    /// </summary>
    public string ProfileId { get; init; } = DefaultProfileId;

    /// <summary>
    /// Gets an explicit finite profile document. When absent, the embedded default profile is used.
    /// </summary>
    public FileInfo? ProfileManifestFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the installed public-header set must equal the locked profile inventory.
    /// </summary>
    public bool RequireLockedHeaderInventory { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether installed packages, CMake, and MSVC must match the profile identity.
    /// </summary>
    public bool RequireLockedToolchain { get; init; } = true;
}