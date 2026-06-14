using System.Diagnostics.CodeAnalysis;

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IRecordManager
{
    void Add(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var record = type.AsCXXRecordDecl;
        if (record is null)
        {
            return;
        }

        Add(record);
    }


    void Add(CXXRecordDecl record);

    bool TryPop([MaybeNullWhen(false)]out CXXRecordDecl record);
}