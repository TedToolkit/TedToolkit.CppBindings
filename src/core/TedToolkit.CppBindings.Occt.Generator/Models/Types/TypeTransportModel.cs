// -----------------------------------------------------------------------
// <copyright file="TypeTransportModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Models.Types;

/// <summary>
/// Describes compiler-derived const and indirection facts for a type.
/// </summary>
/// <param name="ValueIsConst">Whether the terminal value is const-qualified.</param>
/// <param name="Indirections">The indirections ordered outermost to innermost.</param>
internal sealed record TypeTransportModel(
    bool ValueIsConst,
    IReadOnlyList<TypeIndirectionModel> Indirections)
{
    /// <summary>
    /// Gets a non-indirected mutable value transport.
    /// </summary>
    public static TypeTransportModel Value { get; } = new(false, []);
}