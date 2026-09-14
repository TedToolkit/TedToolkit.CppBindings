// -----------------------------------------------------------------------
// <copyright file="TypeIndirectionModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one native type indirection layer.
/// </summary>
/// <param name="Kind">The indirection kind.</param>
/// <param name="IsConstQualified">Whether the layer is const-qualified.</param>
public sealed record TypeIndirectionModel(TypeIndirectionKind Kind, bool IsConstQualified);