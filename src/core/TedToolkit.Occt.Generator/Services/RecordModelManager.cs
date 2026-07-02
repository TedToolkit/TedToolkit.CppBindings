// -----------------------------------------------------------------------
// <copyright file="RecordModelManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Builds and caches projected record and enum models from parsed Clang declarations.
/// </summary>
/// <param name="options">The generation options.</param>
/// <param name="resolver">The type resolver.</param>
/// <param name="defaultsResolver">The default triplet resolver.</param>
/// <param name="vcpkgEnvironment">The vcpkg environment service.</param>
internal sealed class RecordModelManager(
    IOptions<GenerationOptions> options,
    IResolver resolver,
    IVcpkgDefaultTripletResolver defaultsResolver,
    IVcpkgEnvironment vcpkgEnvironment) : IRecordModelManager
{
    private readonly List<EnumModel> _enumModels = [];

    private readonly HashSet<CXCursor> _enumNames = [];

    private readonly Dictionary<CXCursor, RecordModel> _recordNames = [];

    /// <inheritdoc/>
    public IReadOnlyList<EnumModel> EnumModels
    {
        get
        {
            return _enumModels;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<RecordModel> RecordModels
    {
        get
        {
            return _recordNames.Values.Where(t => t.Type.CppTypeName is not "Standard_Transient");
        }
    }

    /// <inheritdoc/>
    public RecordModel Add(CXXRecordDecl record)
    {
        record = record.Definition!;

        if (record is null)
        {
            throw new NotSupportedException("Record is not defined");
        }

        var key = record.CanonicalDecl.Handle;
        if (_recordNames.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var commentProjection = record.ToCommentProjection();
        var type = record.TypeForDecl.Handle;
        type = clang.getCanonicalType(type);
        var size = clang.Type_getSizeOf(type);
        if (size < 0)
        {
            throw new NotSupportedException($"Can't get size of type ({record.TypeForDecl.AsString})");
        }

        record.Location.GetFileLocation(out var file, out _, out _, out _);
        var result = new RecordModel()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            SourceHeader = Path.GetFileName(file.Name.CString),
            Type = resolver.Resolve(record.TypeForDecl)
                .Type,
            Size = size,
            IsAbstract = record.IsAbstract,
        };
        _recordNames.Add(key, result);
        var triplet = options.Value.GetTriplet(defaultsResolver);
        var isOcctType =
            file.Name.CString.Contains(vcpkgEnvironment.GetOcctIncludeFolder(triplet), StringComparison.InvariantCulture);

        result.FieldModels = GetAllDecls(record)
            .SelectMany(r => r.Fields)
            .Where(f => isOcctType || f.Access is CX_CXXAccessSpecifier.CX_CXXPublic)
            .Where(options.Value.FieldTypeToGenerate)
            .Where(static f => IsDefined(f.Type))
            .Select(ToModel)
            .ToArray();

        result.MethodModels = record.Methods
            .Where(m => ShouldIncludeMethod(m, record.IsAbstract))
            .GroupBy(GetMethodSignatureKey)
            .Select(static methods => methods
                .OrderBy(GetConstQualificationWeight)
                .First())
            .Select(ToModel)
            .ToArray();

        result.Base = isOcctType
            ? record.Bases
                .Select(i => i.Type.AsCXXRecordDecl)
                .OfType<CXXRecordDecl>()
                .Select(Add)
                .SingleOrDefault()
            : null;

        return result;
    }

    private TypeModel ToModel(ClangSharp.Type type)
    {
        var result = resolver.Resolve(type);

        if (result.Decl is { } recordDecl)
        {
            Add(recordDecl);
        }

        if (result.Enum is not null)
        {
            Add(result.Enum);
        }

        return result.Type;
    }

    private MethodModel ToModel(CXXMethodDecl method)
    {
        var commentProjection = method.ToCommentProjection();

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            ReturnTypeDescriptionItems = commentProjection.ReturnTypeDescriptionItems,
            ReturnType = ToModel(method.ReturnType),
            MethodName = GetMethodName(method),
            Type = GetMethodType(method),
            Parameters = method.Parameters.Select(p => ToModel(p,
                    commentProjection))
                .ToArray(),
            NoExceptions = IsNoExcept(method),
            IsConst = method.IsConst,
            IsStatic = method.IsStatic,
        };
    }

    private static MethodModelType GetMethodType(CXXMethodDecl method)
    {
        return method switch
        {
            CXXConstructorDecl => MethodModelType.NEW,
            CXXDestructorDecl => MethodModelType.DELETE,
            CXXConversionDecl conversionDecl => IsExplicitConversion(conversionDecl)
                ? MethodModelType.EXPLICIT
                : MethodModelType.IMPLICIT,
            _ when method.IsOverloadedOperator => MethodModelType.OPERATOR,
            _ => MethodModelType.NORMAL,
        };
    }

    private static string GetMethodName(CXXMethodDecl method)
    {
        return GetMethodType(method) switch
        {
            MethodModelType.NEW => "New",
            MethodModelType.DELETE => "Delete",
            MethodModelType.OPERATOR => method.OverloadedOperator switch
            {
                CX_OverloadedOperatorKind.CX_OO_Invalid => "unknown",
                CX_OverloadedOperatorKind.CX_OO_Plus => "+",
                CX_OverloadedOperatorKind.CX_OO_Minus => "-",
                CX_OverloadedOperatorKind.CX_OO_Star => "*",
                CX_OverloadedOperatorKind.CX_OO_Slash => "/",
                CX_OverloadedOperatorKind.CX_OO_Percent => "%",
                CX_OverloadedOperatorKind.CX_OO_Caret => "^",
                CX_OverloadedOperatorKind.CX_OO_Amp => "&",
                CX_OverloadedOperatorKind.CX_OO_Pipe => "|",
                CX_OverloadedOperatorKind.CX_OO_Tilde => "~",
                CX_OverloadedOperatorKind.CX_OO_Exclaim => "!",
                CX_OverloadedOperatorKind.CX_OO_Less => "<",
                CX_OverloadedOperatorKind.CX_OO_Greater => ">",
                CX_OverloadedOperatorKind.CX_OO_PlusEqual => "+=",
                CX_OverloadedOperatorKind.CX_OO_MinusEqual => "-=",
                CX_OverloadedOperatorKind.CX_OO_StarEqual => "*=",
                CX_OverloadedOperatorKind.CX_OO_SlashEqual => "/=",
                CX_OverloadedOperatorKind.CX_OO_PercentEqual => "%=",
                CX_OverloadedOperatorKind.CX_OO_CaretEqual => "^=",
                CX_OverloadedOperatorKind.CX_OO_AmpEqual => "&=",
                CX_OverloadedOperatorKind.CX_OO_PipeEqual => "|=",
                CX_OverloadedOperatorKind.CX_OO_LessLess => "<<",
                CX_OverloadedOperatorKind.CX_OO_GreaterGreater => ">>",
                CX_OverloadedOperatorKind.CX_OO_LessLessEqual => "<<=",
                CX_OverloadedOperatorKind.CX_OO_GreaterGreaterEqual => ">>=",
                CX_OverloadedOperatorKind.CX_OO_EqualEqual => "==",
                CX_OverloadedOperatorKind.CX_OO_ExclaimEqual => "!=",
                CX_OverloadedOperatorKind.CX_OO_LessEqual => "<=",
                CX_OverloadedOperatorKind.CX_OO_GreaterEqual => ">=",
                CX_OverloadedOperatorKind.CX_OO_Spaceship => "<=>",
                CX_OverloadedOperatorKind.CX_OO_AmpAmp => "&&",
                CX_OverloadedOperatorKind.CX_OO_PipePipe => "||",
                CX_OverloadedOperatorKind.CX_OO_PlusPlus => "++",
                CX_OverloadedOperatorKind.CX_OO_MinusMinus => "--",
                CX_OverloadedOperatorKind.CX_OO_Comma => ",",
                CX_OverloadedOperatorKind.CX_OO_Arrow => "->",
                CX_OverloadedOperatorKind.CX_OO_Subscript => "[]",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method.OverloadedOperator,
                    "Unsupported overloaded operator kind."),
            },
            _ => method.Name.ToValidCSharpName(),
        };
    }

    private static bool IsExplicitConversion(CXXConversionDecl conversionDecl)
    {
        return conversionDecl.IsExplicit;
    }

    private static string GetMethodSignatureKey(CXXMethodDecl method)
    {
        return string.Join("|",
        [
            GetMethodType(method).ToString(),
            GetMethodName(method),
            .. method.Parameters.Select(static p => p.Type.ToPInvokeDataType().ToCode()),
        ]);
    }

    private static int GetConstQualificationWeight(CXXMethodDecl method)
    {
        var weight = method.IsConst ? 1 : 0;

        weight += CountConstQualifier(method.ReturnType);
        weight += method.Parameters.Sum(static p => CountConstQualifier(p.Type));

        return weight;
    }

    private static int CountConstQualifier(ClangSharp.Type type)
    {
        var count = 0;
        for (var current = type; ;)
        {
            if (current.IsLocalConstQualified)
            {
                count++;
                current = current.Desugar;
                continue;
            }

            if (current is PointerType or LValueReferenceType or RValueReferenceType)
            {
                current = current.PointeeType;
                continue;
            }

            break;
        }

        return count;
    }

    private static bool IsNoExcept(CXXMethodDecl method)
    {
        if (method.Type is not FunctionProtoType fpt)
        {
            return false;
        }

        return fpt.ExceptionSpecType switch
        {
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_BasicNoexcept => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_ComputedNoexcept => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_NoThrow => true,
            CXCursor_ExceptionSpecificationKind.CXCursor_ExceptionSpecificationKind_DynamicNone => true,
            _ => false,
        };
    }

    private ParameterModel ToModel(ParmVarDecl paramDel, CommentProjection methodCommentProjection)
    {
        methodCommentProjection.ParameterDescriptionItems.TryGetValue(paramDel.Name, out var descriptionItems);

        return new()
        {
            DescriptionItems = descriptionItems ?? [],
            Type = ToModel(paramDel.Type),
            Name = paramDel.Name,
        };
    }

    private FieldModel ToModel(FieldDecl fieldDecl)
    {
        var commentProjection = fieldDecl.ToCommentProjection();

        var offset = fieldDecl.Handle.OffsetOfField / 8;
        if (offset < 0)
        {
            throw new NotSupportedException(
                $"Can't get offset of field ({fieldDecl.Name} in {fieldDecl.Parent?.Name})");
        }

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            Name = fieldDecl.Name,
            Type = ToModel(fieldDecl.Type),
            Offset = offset,
        };
    }

    private static bool IsDefined(ClangSharp.Type type)
    {
        var addingType = type.CanonicalType.GetAddingType()?.CanonicalType;

        if (addingType is null)
        {
            return false;
        }

        if (addingType.Kind is CXTypeKind.CXType_LongDouble
            or CXTypeKind.CXType_Overload
            or CXTypeKind.CXType_Dependent
            or CXTypeKind.CXType_ObjCId
            or CXTypeKind.CXType_ObjCClass
            or CXTypeKind.CXType_ObjCSel
            or CXTypeKind.CXType_Float128
            or CXTypeKind.CXType_ShortAccum
            or CXTypeKind.CXType_Accum
            or CXTypeKind.CXType_LongAccum
            or CXTypeKind.CXType_UShortAccum
            or CXTypeKind.CXType_UAccum
            or CXTypeKind.CXType_ULongAccum
            or CXTypeKind.CXType_BFloat16
            or CXTypeKind.CXType_Ibm128)
        {
            return false;
        }

        if (addingType is BuiltinType)
        {
            return true;
        }

        var result = addingType.AsCXXRecordDecl?.Definition is not null;
        if (!result)
        {
        }

        return result;
    }

    private static bool ShouldIncludeMethod(CXXMethodDecl method, bool isAbstract)
    {
        if (!IsDefined(method.ReturnType))
        {
            return false;
        }

        if (method.Parameters.Any(p => !IsDefined(p.Type) || string.IsNullOrEmpty(p.Name)))
        {
            return false;
        }

        if (method.Access is not CX_CXXAccessSpecifier.CX_CXXPublic)
        {
            return false;
        }

        if (method.OverloadedOperator
            is CX_OverloadedOperatorKind.CX_OO_Call
            or CX_OverloadedOperatorKind.CX_OO_Equal)
        {
            return false;
        }

        if (IsOperatorNewOrDelete(method))
        {
            return false;
        }

        if (isAbstract && method is CXXConstructorDecl or CXXDestructorDecl)
        {
            return false;
        }

        return true;
    }

    private static bool IsOperatorNewOrDelete(CXXMethodDecl method)
    {
        if (!method.IsOverloadedOperator)
        {
            return false;
        }

        return method.OverloadedOperator switch
        {
            CX_OverloadedOperatorKind.CX_OO_New => true,
            CX_OverloadedOperatorKind.CX_OO_Delete => true,
            CX_OverloadedOperatorKind.CX_OO_Array_New => true,
            CX_OverloadedOperatorKind.CX_OO_Array_Delete => true,
            _ => false,
        };
    }

    private static IEnumerable<CXXRecordDecl> GetAllDecls(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var bases in GetAllDecls(baseDecl))
            {
                yield return bases;
            }
        }

        yield return record;
    }

    private void Add(EnumDecl enumModel)
    {
        if (!_enumNames.Add(enumModel.CanonicalDecl.Handle))
        {
            return;
        }

        _enumModels.Add(CreateEnumModel(enumModel));
    }

    private static EnumModel CreateEnumModel(EnumDecl enumDecl)
    {
        if (enumDecl.IntegerType is not BuiltinType builtinType)
        {
            throw new NotSupportedException($"Unsupported enum underlying type ({enumDecl.IntegerType.AsString})");
        }

        var underlyingType = builtinType.ToDataType();

        return new()
        {
            DescriptionItems = enumDecl.ToCommentProjection().DescriptionItems,
            Name = enumDecl.Name.ToValidCSharpName(),
            SourceType = enumDecl.TypeForDecl.AsString,
            UnderlyingType = underlyingType,
            Members = enumDecl.Enumerators.Select(ToEnumMember).ToArray(),
        };
    }

    private static EnumMemberModel ToEnumMember(EnumConstantDecl enumConstant)
    {
        return new()
        {
            DescriptionItems = enumConstant.ToCommentProjection().DescriptionItems,
            Name = enumConstant.Name,
            Value = enumConstant.IsUnsigned
                ? enumConstant.UnsignedInitVal.ToLiteral()
                : enumConstant.InitVal.ToLiteral(),
        };
    }
}