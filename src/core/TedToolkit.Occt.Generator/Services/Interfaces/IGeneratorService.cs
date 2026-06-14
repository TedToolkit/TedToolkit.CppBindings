using ClangSharp;

using TedToolkit.Occt.Generator.Generators;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IGeneratorService
{
    CSharpGenerator GenerateCSharp(CXXRecordDecl record);

    CppGenerator GenerateCpp(CXXRecordDecl record);
}