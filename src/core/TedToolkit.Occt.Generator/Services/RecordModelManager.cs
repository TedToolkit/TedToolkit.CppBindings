using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class RecordModelManager(IOptions<GenerationOptions> options, IResolver resolver) : IRecordModelManager
{
    private readonly List<RecordModel> _recordModels = [];

    private readonly HashSet<string> _recordNames = [];

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
            Type = resolver.Resolve(type,
                out _),
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
        var result = resolver.Resolve(type.CanonicalType, out var recordDecl);
        if (recordDecl is null)
        {
            return result;
        }

        Add(recordDecl);
        return result;
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
}