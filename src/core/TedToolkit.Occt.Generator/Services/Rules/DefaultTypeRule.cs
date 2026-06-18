using System.Globalization;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Services.Rules;

public class DefaultTypeRule : ITypeRule
{
    public bool TryResolve(ClangSharp.Type type, out TypeResolveResult result)
    {
        ArgumentNullException.ThrowIfNull(type);

        var canonicalType = type.CanonicalType;
        var enumModel = TryGetEnumDecl(canonicalType, out var enumDecl)
            ? CreateEnumModel(enumDecl)
            : null;

        result = new TypeResolveResult
        {
            Decl = canonicalType.AsCXXRecordDecl,
            Type = enumModel is null
                ? new TypeModel
                {
                    SourceType = type.AsString,
                    CppInteropType = type.AsString,
                    CSharpPInvokeType = new(type.AsString),
                    CSharpPublicType = new(ToValidCSharpName(type.AsString)),
                }
                : new TypeModel
                {
                    SourceType = type.AsString,
                    CppInteropType = enumDecl.Name,
                    CSharpPInvokeType = enumModel.UnderlyingType,
                    CSharpPublicType = new(enumModel.Name),
                },
            Enum = enumModel,
        };

        return true;
    }

    private static EnumModel CreateEnumModel(EnumDecl enumDecl)
    {
        var enumName = ToValidCSharpName(enumDecl.Name);
        var underlyingType = GetUnderlyingType(enumDecl.IntegerType);

        return new EnumModel
        {
            Name = enumName,
            SourceType = enumDecl.TypeForDecl.AsString,
            UnderlyingType = underlyingType,
            Members = enumDecl.Enumerators.Select(ToEnumMember).ToArray(),
        };
    }

    private static bool TryGetEnumDecl(ClangSharp.Type type, out EnumDecl enumDecl)
    {
        enumDecl = type switch
        {
            EnumType enumType => enumType.Decl,
            _ => type.AsTagDecl as EnumDecl
                 ?? type.CanonicalType.AsTagDecl as EnumDecl
                 ?? type.Desugar.AsTagDecl as EnumDecl,
        } ?? null!;

        return enumDecl is not null;
    }

    private static EnumMemberModel ToEnumMember(EnumConstantDecl enumConstant)
    {
        return new EnumMemberModel
        {
            Name = enumConstant.Name,
            Value = enumConstant.IsUnsigned
                ? enumConstant.UnsignedInitVal.ToString(CultureInfo.InvariantCulture)
                : enumConstant.InitVal.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static DataType GetUnderlyingType(ClangSharp.Type type)
    {
        return new(type.Kind switch
        {
            CXTypeKind.CXType_Bool => "bool",
            CXTypeKind.CXType_Char_U or CXTypeKind.CXType_UChar => "byte",
            CXTypeKind.CXType_Char16 => "char",
            CXTypeKind.CXType_UShort => "ushort",
            CXTypeKind.CXType_UInt => "uint",
            CXTypeKind.CXType_ULong or CXTypeKind.CXType_ULongLong => "ulong",
            CXTypeKind.CXType_Char_S or CXTypeKind.CXType_SChar => "sbyte",
            CXTypeKind.CXType_WChar or CXTypeKind.CXType_Short => "short",
            CXTypeKind.CXType_Int => "int",
            CXTypeKind.CXType_Long or CXTypeKind.CXType_LongLong => "long",
            _ => throw new NotSupportedException($"Unsupported enum underlying type ({type.AsString})"),
        });
    }

    private static string ToValidCSharpName(string name)
    {
        using var builder = ZString.CreateStringBuilder();
        var last = false;
        foreach (var c in name)
        {
            if (builder.Length is 0 && char.IsNumber(c))
            {
                AppendUnderscore();
            }
            else if (char.IsLetterOrDigit(c))
            {
                Append(c);
            }
            else
            {
                AppendUnderscore();
            }
        }

        if (last)
        {
            builder.Remove(builder.Length - 1, 1);
        }

        return builder.ToString();

        void Append(char c)
        {
            builder.Append(c);
            last = false;
        }

        void AppendUnderscore()
        {
            if (last)
            {
                return;
            }

            builder.Append('_');
            last = true;
        }
    }
}
