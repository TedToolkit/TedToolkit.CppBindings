// -----------------------------------------------------------------------
// <copyright file="Resolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using ClangSharp;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Models.Types;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

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
        var transport = TypeTransportModel.FromClang(type, out var terminalType);
        type = type.CanonicalType;

        if (TryGetEnumDecl(terminalType, out var enumDecl)
            && transport.Indirections.Count is 0)
        {
            return Complete(new()
            {
                Decl = type.AsCXXRecordDecl,
                Type = new()
                {
                    CppTypeName = enumDecl.QualifiedName,
                    CSharpPInvokeType = enumDecl.IntegerType.ToPInvokeDataType(),
                    CSharpPublicType = new(enumDecl.QualifiedName.ToValidCSharpName()),
                },
                Enum = enumDecl,
            }, terminalType, transport);
        }

        foreach (var typeRule in typeRules)
        {
            if (typeRule.TryResolve(type, out var result))
            {
                return Complete(result, terminalType, transport);
            }
        }

        return Complete(new()
        {
            Decl = terminalType.AsCXXRecordDecl,
            Type = new()
            {
                CppTypeName = type.AsString,
                CSharpPInvokeType = type.ToPInvokeDataType(),
                CSharpPublicType = type.ToPublicDataType(),
            },
        }, terminalType, transport);
    }

    private static TypeResolveResult Complete(
        TypeResolveResult result,
        ClangSharp.Type terminalType,
        TypeTransportModel transport)
    {
        result.Type.CppValueTypeName = GetUnqualifiedValueTypeName(terminalType.AsString);
        result.Type.IsRecord = terminalType.AsCXXRecordDecl is not null;
        result.Type.Transport = transport;
        return result;
    }

    private static string GetUnqualifiedValueTypeName(string typeName)
    {
        const string Prefix = "const ";
        const string Suffix = " const";
        var result = typeName.Trim();
        if (result.StartsWith(Prefix, StringComparison.Ordinal))
        {
            result = result[Prefix.Length..];
        }

        if (result.EndsWith(Suffix, StringComparison.Ordinal))
        {
            result = result[..^Suffix.Length];
        }

        return result;
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