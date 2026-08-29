// -----------------------------------------------------------------------
// <copyright file="Resolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using ClangSharp;

using TedToolkit.Occt.Generator.Models.Types;
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
        var transport = CreateTransport(type, out var terminalType);
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

    /// <summary>
    /// Extracts structural const and indirection facts from a compiler type.
    /// </summary>
    /// <param name="type">The compiler type.</param>
    /// <param name="terminalType">The type after removing pointer and reference layers.</param>
    /// <returns>The structural transport facts.</returns>
    /// <exception cref="InvalidOperationException">The compiler exposes an unsupported indirection.</exception>
    internal static TypeTransportModel CreateTransport(
        ClangSharp.Type type,
        out ClangSharp.Type terminalType)
    {
        var indirections = new List<TypeIndirectionModel>();
        var current = type;
        while (current.CanonicalType is PointerType or LValueReferenceType or RValueReferenceType)
        {
            var canonical = current.CanonicalType;
            var kind = canonical switch
            {
                PointerType => TypeIndirectionKind.Pointer,
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