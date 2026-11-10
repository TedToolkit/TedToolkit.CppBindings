// -----------------------------------------------------------------------
// <copyright file="TypeTransportModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

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

    /// <summary>
    /// Reads the qualifiers and indirections of a Clang type.
    /// </summary>
    /// <param name="type">The compiler type.</param>
    /// <param name="terminalType">The canonical type after removing the indirections.</param>
    /// <returns>The structural transport facts.</returns>
    /// <exception cref="InvalidOperationException">The compiler exposes an unsupported indirection.</exception>
    public static TypeTransportModel FromClang(ClangSharp.Type type, out ClangSharp.Type terminalType)
    {
        ArgumentNullException.ThrowIfNull(type);
        var indirections = new List<TypeIndirectionModel>();
        var current = type;
        while (current.CanonicalType is PointerType or LValueReferenceType or RValueReferenceType)
        {
            var canonical = current.CanonicalType;
            var kind = canonical switch
            {
                PointerType => TypeIndirectionKind.PointerIndirection,
                LValueReferenceType => TypeIndirectionKind.LValueReference,
                RValueReferenceType => TypeIndirectionKind.RValueReference,
                _ => throw new InvalidOperationException("Unsupported native indirection."),
            };
            indirections.Add(new(kind, current.IsLocalConstQualified || canonical.IsLocalConstQualified));
            current = canonical.PointeeType;
        }

        terminalType = current.CanonicalType;
        return new(current.IsLocalConstQualified || terminalType.IsLocalConstQualified, indirections);
    }

    /// <summary>
    /// Gets the outermost indirection, if any.
    /// </summary>
    internal TypeIndirectionKind? FirstIndirection
    {
        get
        {
            return Indirections.Count is 0 ? null : Indirections[0].Kind;
        }
    }

    /// <summary>
    /// Gets whether the value immediately behind the outermost indirection is const.
    /// </summary>
    internal bool ReferencedValueIsConst
    {
        get
        {
            return Indirections.Count > 1 ? Indirections[1].IsConstQualified : ValueIsConst;
        }
    }
}