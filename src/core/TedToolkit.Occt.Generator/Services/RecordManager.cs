using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using ClangSharp;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class RecordManager(ITypeService typeService, IRecordService recordService) : IRecordManager
{
    private readonly ConcurrentDictionary<string, byte> _keys = [];

    private readonly ConcurrentQueue<CXXRecordDecl> _decls = [];

    public void Add(CXXRecordDecl record)
    {
        var name = typeService.GetCppName(recordService.GetType(record));
        if (!_keys.TryAdd(name, 0))
        {
            return;
        }

        _decls.Enqueue(record);
    }

    public bool TryPop([MaybeNullWhen(false)] out CXXRecordDecl record)
    {
        return _decls.TryDequeue(out record);
    }
}