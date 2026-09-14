// -----------------------------------------------------------------------
// <copyright file="ManifoldSourceDisposition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Describes one installed Manifold header and its finite-profile reachability.</summary>
internal sealed record ManifoldSourceDisposition(string Header, string Disposition);