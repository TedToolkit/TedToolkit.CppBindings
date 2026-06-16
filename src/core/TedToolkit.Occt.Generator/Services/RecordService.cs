using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class RecordService : IRecordService
{
    public string GetName(CXXRecordDecl decl)
    {
        return decl.Name;
    }

    public ClangSharp.Type GetType(CXXRecordDecl decl)
    {
        return decl.TypeForDecl;
    }

    public IEnumerable<FieldDecl> GetFields(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var fieldDecl in GetFields(baseDecl))
            {
                yield return fieldDecl;
            }
        }

        foreach (var recordField in record.Fields)
        {
            yield return recordField;
        }
    }

    public IEnumerable<CXXMethodDecl> GetMethods(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var methodDecl in GetMethods(baseDecl))
            {
                yield return methodDecl;
            }
        }

        foreach (var recordMethod in record.Methods)
        {
            yield return recordMethod;
        }
    }

    public async Task<long> GetSizeAsync(CXXRecordDecl record)
    {
        var type = record.TypeForDecl.Handle;
        type = clang.getCanonicalType(type);
        var size = clang.Type_getSizeOf(type);

        if (size < 0)
        {
            throw new InvalidOperationException($"Cannot get sizeof record ({GetName(record)}): {size}");
        }

        return size;
    }
}