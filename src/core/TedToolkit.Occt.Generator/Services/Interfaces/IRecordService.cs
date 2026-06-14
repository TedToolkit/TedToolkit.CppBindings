using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IRecordService : IDeclService<CXXRecordDecl>
{
    IEnumerable<FieldDecl> GetFields(CXXRecordDecl record);

    IEnumerable<CXXMethodDecl> GetMethods(CXXRecordDecl record);
}