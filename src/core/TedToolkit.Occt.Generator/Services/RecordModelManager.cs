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

    private readonly List<RecordModel> _recordModels = [];

    private readonly HashSet<CXCursor> _enumNames = [];

    private readonly HashSet<CXCursor> _recordNames = [];

    public IReadOnlyList<EnumModel> EnumModels => _enumModels;

    public IReadOnlyList<RecordModel> RecordModels
    {
        get
        {
            return _recordModels;
        }
    }

    public void Add(CXXRecordDecl record)
    {
        record = record.Definition ?? record;
        ArgumentNullException.ThrowIfNull(record);
        if (!_recordNames.Add(record.CanonicalDecl.Handle))
        {
            return;
        }

        var commentProjection = record.ToCommentProjection();
        _recordModels.Add(new RecordModel
        {
            DescriptionItems = commentProjection.DescriptionItems,
            Type = resolver.Resolve(record.TypeForDecl).Type,
            FieldModels = GetAllDecls(record)
                .SelectMany(r => r.Fields)
                .Where(options.Value.FieldTypeToGenerate)
                .Select(ToModel)
                .ToArray(),
            MethodModels = GetAllDecls(record)
                .SelectMany(r => r.Methods)
                .Select(ToModel)
                .ToArray(),
            BaseTypes = record.Bases
                .Select(b => ToModel(b.Type))
                .ToArray(),
        });
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
            MethodName = method.Name,
            Parameters = method.Parameters.Select(p => ToModel(p, commentProjection))
                .ToArray(),
            NoExceptions = IsNoExcept(method),
        };
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

        return new()
        {
            DescriptionItems = commentProjection.DescriptionItems,
            Name = fieldDecl.Name,
            Type = ToModel(fieldDecl.Type),
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
}
