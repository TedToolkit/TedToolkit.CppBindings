using System.Diagnostics.CodeAnalysis;

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IRecordManager
{
    bool Add(CXXRecordDecl record);

    bool TryPop([MaybeNullWhen(false)]out CXXRecordDecl record);
}