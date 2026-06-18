using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class RecordModelManager(IOptions<GenerationOptions> options, IResolver resolver) : IRecordModelManager
{
    private readonly List<EnumModel> _enumModels = [];

    private readonly List<RecordModel> _recordModels = [];

    private readonly HashSet<string> _enumNames = [];

    private readonly HashSet<string> _recordNames = [];

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
        var type = record.TypeForDecl.CanonicalType;
        var typeName = type.AsString;
        if (!_recordNames.Add(typeName))
        {
            return;
        }

        _recordModels.Add(new RecordModel
        {
            Type = resolver.Resolve(type).Type,
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
        var canonicalType = type.CanonicalType;
        var result = resolver.Resolve(canonicalType);

        if (result.Decl is { } recordDecl)
        {
            Add(recordDecl);
        }

        if (result.Enum is not null)
        {
            AddEnum(result.Enum);
        }

        return result.Type;
    }

    private MethodModel ToModel(CXXMethodDecl method)
    {
        return new()
        {
            ReturnType = ToModel(method.ReturnType),
            MethodName = method.Name,
            Parameters = method.Parameters.Select(ToModel).ToArray(),
        };
    }

    private ParameterModel ToModel(ParmVarDecl paramDel)
    {
        return new() { Type = ToModel(paramDel.Type), Name = paramDel.Name, };
    }

    private FieldModel ToModel(FieldDecl fieldDecl)
    {
        return new() { Name = fieldDecl.Name, Type = ToModel(fieldDecl.Type), };
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

    private void AddEnum(EnumModel enumModel)
    {
        if (_enumNames.Add(enumModel.SourceType) is false)
        {
            return;
        }

        _enumModels.Add(enumModel);
    }
}
