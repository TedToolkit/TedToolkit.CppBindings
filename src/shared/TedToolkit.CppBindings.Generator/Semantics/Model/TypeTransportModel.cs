// -----------------------------------------------------------------------
// <copyright file="TypeTransportModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes compiler-derived const and indirection facts for a type.
/// </summary>
public sealed class TypeTransportModel
{
    /// <summary>
    /// Initializes a copied transport descriptor.
    /// </summary>
    /// <param name="valueIsConst">Whether the terminal value is const-qualified.</param>
    /// <param name="indirections">The indirections ordered outermost to innermost.</param>
    public TypeTransportModel(bool valueIsConst, IEnumerable<TypeIndirectionModel> indirections)
    {
        ArgumentNullException.ThrowIfNull(indirections);
        ValueIsConst = valueIsConst;
        Indirections = Array.AsReadOnly(indirections.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the terminal value is const-qualified.
    /// </summary>
    public bool ValueIsConst { get; }

    /// <summary>
    /// Gets the copied indirections ordered outermost to innermost.
    /// </summary>
    public IReadOnlyList<TypeIndirectionModel> Indirections { get; }

    /// <summary>
    /// Gets a non-indirected mutable value transport.
    /// </summary>
    public static TypeTransportModel Value { get; } = new(false, []);
}