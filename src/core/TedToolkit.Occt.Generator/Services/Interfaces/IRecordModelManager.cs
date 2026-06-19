using ClangSharp;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IRecordModelManager
{
    RecordModel Add(CXXRecordDecl record);

    IReadOnlyList<EnumModel> EnumModels { get; }

    IReadOnlyCollection<RecordModel> RecordModels { get; }
}
