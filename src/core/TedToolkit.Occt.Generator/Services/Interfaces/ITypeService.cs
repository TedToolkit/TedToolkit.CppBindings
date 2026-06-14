namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface ITypeService
{
    string GetCppName(ClangSharp.Type type);

    string GetCSharpName(ClangSharp.Type type);

    ClangSharp.Type DesugarType(ClangSharp.Type type);
}