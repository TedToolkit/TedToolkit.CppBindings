// -----------------------------------------------------------------------
// <copyright file="CgalSourceDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Records why one installed public header does or does not participate in declaration discovery.
/// </summary>
/// <param name="Header">The path relative to the vcpkg include root.</param>
/// <param name="Disposition">The stable finite-profile source disposition.</param>
public sealed record CgalSourceDisposition(string Header, string Disposition);