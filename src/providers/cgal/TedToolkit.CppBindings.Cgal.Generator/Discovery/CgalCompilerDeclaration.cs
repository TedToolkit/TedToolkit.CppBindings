// -----------------------------------------------------------------------
// <copyright file="CgalCompilerDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Describes one public declaration reported by Clang from the finite header closure.
/// </summary>
/// <param name="Identity">The stable compiler identity.</param>
/// <param name="Signature">The compiler spelling and type.</param>
/// <param name="Header">The declaring public header.</param>
/// <param name="Kind">The Clang cursor kind.</param>
/// <param name="Name">The unqualified declaration spelling.</param>
internal sealed record CgalCompilerDeclaration(
    string Identity,
    string Signature,
    string Header,
    string Kind,
    string Name);