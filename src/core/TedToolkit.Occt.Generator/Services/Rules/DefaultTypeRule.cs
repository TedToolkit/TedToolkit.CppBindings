using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Services.Rules;

public class DefaultTypeRule : ITypeRule
{
    public bool TryResolve(ClangSharp.Type type, out TypeResolveResult result)
    {
        ArgumentNullException.ThrowIfNull(type);

        result = new TypeResolveResult
        {
            Decl = type.AsCXXRecordDecl,
            Type = TryGetEnumDecl(type, out var enumDecl)
                ? new TypeModel
                {
                    SourceType = enumDecl.Name,
                    CppInteropType = enumDecl.Name,
                    CSharpPInvokeType = new(enumDecl.Name),
                    CSharpPublicType = new(enumDecl.Name),
                }
                : new TypeModel
                {
                    SourceType = type.AsString,
                    CppInteropType = type.AsString,
                    CSharpPInvokeType = new(type.AsString),
                    CSharpPublicType = new(type.AsString.ToValidCSharpName()),
                },
            Enum = enumDecl,
        };

        return true;
    }

    private static bool TryGetEnumDecl(ClangSharp.Type type, [NotNullWhen(true)] out EnumDecl? enumDecl)
    {
        enumDecl = type switch
        {
            EnumType enumType => enumType.Decl,
            _ => type.AsTagDecl as EnumDecl,
        } ?? null;

        return enumDecl is not null;
    }
}