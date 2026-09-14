// -----------------------------------------------------------------------
// <copyright file="CgalArtifactInventoryEntry.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Binds one admitted declaration to one generated artifact and symbol.
/// </summary>
/// <param name="DeclarationId">The stable finite-profile declaration identity.</param>
/// <param name="RelativePath">The generated artifact path.</param>
/// <param name="Symbol">The generated managed member or native export.</param>
public sealed record CgalArtifactInventoryEntry(string DeclarationId, string RelativePath, string Symbol);