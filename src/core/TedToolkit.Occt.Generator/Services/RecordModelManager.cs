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

internal sealed class RecordModelManager(IOptions<GenerationOptions> options, IResolver resolver) : IRecordModelManager
{
    private readonly List<EnumModel> _enumModels = [];
    private readonly HashSet<CXCursor> _enumNames = [];
    private readonly HashSet<CXCursor> _recordsInProgress = [];
    private readonly Dictionary<CXCursor, RecordModel> _recordNames = [];

    public IReadOnlyList<EnumModel> EnumModels => _enumModels;

    public IEnumerable<RecordModel> RecordModels
    {
        get
        {
            return _recordNames.Values.Where(t => t.Type.CppTypeName is not "Standard_Transient");
        }
    }

    public RecordModel Add(CXXRecordDecl record)
    {
        record = record.Definition ?? record;
        ArgumentNullException.ThrowIfNull(record);

        var key = record.CanonicalDecl.Handle;
        if (_recordNames.TryGetValue(key, out var existing))
        {
            return existing;
        }

        if (!_recordsInProgress.Add(key))
        {
            throw new InvalidOperationException($"Record '{record.Name}' was requested before model construction completed.");
        }

        try
        {
            var commentProjection = record.ToCommentProjection();
#pragma warning disable RCS1212
            var result = new RecordModel
            {
                DescriptionItems = commentProjection.DescriptionItems,
                Type = resolver.Resolve(record.TypeForDecl)
                    .Type,
                FieldModels = GetAllDecls(record)
                    .SelectMany(r => r.Fields)
                    .Where(options.Value.FieldTypeToGenerate)
                    .Select(ToModel)
                    .ToArray(),
                MethodModels =
                    record.Methods.Where(ShouldIncludeMethod)
                        .Select(ToModel)
                        .ToArray(),
                BaseTypes = record.Bases
                    .Select(b => ToModel(b.Type))
                    .ToArray(),
                Bases = record.Bases
                    .Select(i => i.Type.AsCXXRecordDecl)
                    .OfType<CXXRecordDecl>()
                    .Select(Add)
                    .OfType<RecordModel>()
                    .ToArray(),
            };
#pragma warning restore RCS1212
            _recordNames.Add(key, result);
            return result;
        }
        finally
        {
            _recordsInProgress.Remove(key);
        }
    }

    private TypeModel ToModel(ClangSharp.Type type)
    {
        var result = resolver.Resolve(type);

        if (result.Decl is { } recordDecl)
        {
            TryAddReferencedRecord(recordDecl);
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
        };
    }

    private static MethodModelType GetMethodType(CXXMethodDecl method)
    {
        return method switch
        {
            CXXConstructorDecl => MethodModelType.New,
            CXXDestructorDecl => MethodModelType.Delete,
            CXXConversionDecl conversionDecl => IsExplicitConversion(conversionDecl)
                ? MethodModelType.Explicit
                : MethodModelType.Implicit,
            _ when method.IsOverloadedOperator => MethodModelType.Operator,
            _ => MethodModelType.Normal,
        };
    }

    private static string GetMethodName(CXXMethodDecl method)
    {
        return GetMethodType(method) switch
        {
            MethodModelType.New => "New",
            MethodModelType.Delete => "Delete",
            MethodModelType.Operator or MethodModelType.Implicit or MethodModelType.Explicit => method.Name,
            _ => method.Name.ToValidCSharpName(),
        };
    }

    private static bool IsExplicitConversion(CXXConversionDecl conversionDecl)
    {
        return conversionDecl.IsExplicit;
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
            DescriptionItems = descriptionItems ?? [], Type = ToModel(paramDel.Type), Name = paramDel.Name,
        };
    }

    private FieldModel ToModel(FieldDecl fieldDecl)
    {
        var commentProjection = fieldDecl.ToCommentProjection();

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            Name = fieldDecl.Name,
            Type = ToModel(fieldDecl.Type),
        };
    }

    private static bool ShouldIncludeMethod(CXXMethodDecl method)
    {
        return !IsOperatorNewOrDelete(method);
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

        return new EnumModel
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
        return new EnumMemberModel
        {
            DescriptionItems = enumConstant.ToCommentProjection().DescriptionItems,
            Name = enumConstant.Name,
            Value = enumConstant.IsUnsigned
                ? enumConstant.UnsignedInitVal.ToLiteral()
                : enumConstant.InitVal.ToLiteral(),
        };
    }

    private void TryAddReferencedRecord(CXXRecordDecl recordDecl)
    {
        recordDecl = recordDecl.Definition ?? recordDecl;
        var key = recordDecl.CanonicalDecl.Handle;
        if (_recordNames.ContainsKey(key) || _recordsInProgress.Contains(key))
        {
            return;
        }

        Add(recordDecl);
    }
}
