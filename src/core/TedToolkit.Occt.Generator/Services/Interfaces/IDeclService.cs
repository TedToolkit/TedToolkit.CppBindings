using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IDeclService<TDecl>
    where TDecl : Decl
{
    string GetName(TDecl decl);

    ClangSharp.Type GetType(TDecl decl);
}