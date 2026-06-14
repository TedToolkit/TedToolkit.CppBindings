using ClangSharp;

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
}