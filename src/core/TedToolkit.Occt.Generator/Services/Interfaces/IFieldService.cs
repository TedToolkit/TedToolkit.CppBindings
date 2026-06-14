using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IFieldService : IDeclService<FieldDecl>
{
    ValueTask<long> GetOffsetAsync(FieldDecl field);
}