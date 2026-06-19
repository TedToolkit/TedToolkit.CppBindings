using ClangSharp;

namespace TedToolkit.Occt.Generator.Models;

public sealed class TypeResolveResult
{
    public CXXRecordDecl? Decl { get; init; }
    public EnumDecl? Enum { get; init; }

    public required TypeModel Type { get; init; }

}
