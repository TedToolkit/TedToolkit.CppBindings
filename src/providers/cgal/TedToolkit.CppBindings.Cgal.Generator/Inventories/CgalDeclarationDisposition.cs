// -----------------------------------------------------------------------
// <copyright file="CgalDeclarationDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Records the discovered disposition and narrow proof for one finite declaration candidate.
/// </summary>
/// <param name="Id">The stable declaration identity.</param>
/// <param name="NativeSignature">The requested closed native signature.</param>
/// <param name="Header">The declaring public header.</param>
/// <param name="Kind">The declaration category.</param>
/// <param name="Disposition">The admitted or unsupported disposition.</param>
/// <param name="Proof">The narrow discovered proof.</param>
public sealed record CgalDeclarationDisposition(
    string Id,
    string NativeSignature,
    string Header,
    string Kind,
    string Disposition,
    string Proof);