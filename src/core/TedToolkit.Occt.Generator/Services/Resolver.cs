// -----------------------------------------------------------------------
// <copyright file="Resolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using ClangSharp;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Resolves clang types into projection models by applying ordered rules.
/// </summary>
/// <param name="typeRules">The ordered projection rules.</param>
internal sealed class Resolver(IEnumerable<ITypeRule> typeRules) : IResolver
{
    /// <inheritdoc/>
    public TypeResolveResult Resolve(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        type = type.CanonicalType;

        if (TryGetEnumDecl(type, out var enumDecl))
        {
            return new()
            {
                Decl = type.AsCXXRecordDecl,
                Type = new()
                {
                    CppTypeName = enumDecl.Name,
                    CSharpPInvokeType = new(enumDecl.Name),
                    CSharpPublicType = new(enumDecl.Name),
                },
                Enum = enumDecl,
            };
        }

        foreach (var typeRule in typeRules)
        {
            if (typeRule.TryResolve(type, out var result))
            {
                return result;
            }
        }

        return new()
        {
            Decl = type.GetAddingType()?.AsCXXRecordDecl,
            Type = new()
            {
                CppTypeName = type.AsString,
                CSharpPInvokeType = type.ToPInvokeDataType(),
                CSharpPublicType = type.ToPublicDataType(),
            },
        };
    }

    private static bool TryGetEnumDecl(ClangSharp.Type type, [NotNullWhen(true)] out EnumDecl? enumDecl)
    {
        enumDecl = type switch
        {
            EnumType enumType => enumType.Decl,
            _ => type.AsTagDecl as EnumDecl,
        };

        return enumDecl is not null;
    }
}