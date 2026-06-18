using ClangSharp;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IRecordModelManager
{
    void Add(CXXRecordDecl record);

    IReadOnlyList<EnumModel> EnumModels { get; }

    IReadOnlyList<RecordModel> RecordModels { get; }
}
