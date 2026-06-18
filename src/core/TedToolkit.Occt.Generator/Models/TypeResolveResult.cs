using ClangSharp;

namespace TedToolkit.Occt.Generator.Models;

public sealed class TypeResolveResult
{
    public CXXRecordDecl? Decl { get; init; }

    public required TypeModel Type { get; init; }

    public EnumModel? Enum { get; init; }
}
