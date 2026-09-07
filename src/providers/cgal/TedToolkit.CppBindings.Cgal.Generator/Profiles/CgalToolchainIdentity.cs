// -----------------------------------------------------------------------
// <copyright file="CgalToolchainIdentity.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Records the compiler and dependency matrix owned by a profile.
/// </summary>
public sealed record CgalToolchainIdentity
{
    /// <summary>
    /// Gets the required MSVC compiler version.
    /// </summary>
    public required string Msvc { get; init; }

    /// <summary>
    /// Gets the required CMake version.
    /// </summary>
    public required string Cmake { get; init; }

    /// <summary>
    /// Gets the required GMP vcpkg version.
    /// </summary>
    public required string Gmp { get; init; }

    /// <summary>
    /// Gets the required MPFR vcpkg version.
    /// </summary>
    public required string Mpfr { get; init; }
}