// -----------------------------------------------------------------------
// <copyright file="CgalResolvedToolchain.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Records the installed package and native-tool identities used by one generation run.
/// </summary>
/// <param name="Cgal">The installed CGAL version.</param>
/// <param name="CgalAbi">The installed CGAL vcpkg ABI.</param>
/// <param name="Gmp">The installed GMP version and port revision.</param>
/// <param name="GmpAbi">The installed GMP vcpkg ABI.</param>
/// <param name="Mpfr">The installed MPFR version and port revision.</param>
/// <param name="MpfrAbi">The installed MPFR vcpkg ABI.</param>
/// <param name="CMake">The resolved CMake version.</param>
/// <param name="Msvc">The resolved MSVC compiler version.</param>
public sealed record CgalResolvedToolchain(
    string Cgal,
    string CgalAbi,
    string Gmp,
    string GmpAbi,
    string Mpfr,
    string MpfrAbi,
    string CMake,
    string Msvc);