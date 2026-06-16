// -----------------------------------------------------------------------
// <copyright file="RecordManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using ClangSharp;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Tracks the unique record declarations queued for generation.
/// </summary>
/// <param name="typeService">The type naming service.</param>
/// <param name="recordService">The record metadata service.</param>
public sealed class RecordManager(ITypeService typeService, IRecordService recordService) : IRecordManager
{
    private readonly ConcurrentDictionary<string, byte> _keys = [];

    private readonly ConcurrentQueue<CXXRecordDecl> _decls = [];

    /// <inheritdoc/>
    public bool Add(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);

        record = record.Definition ?? record;
        var name = typeService.GetCppName(recordService.GetType(record));
        if (!_keys.TryAdd(name, 0))
        {
            return false;
        }

        _decls.Enqueue(record);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<CXXRecordDecl> GetRecords()
    {
        return _decls.ToArray();
    }

    /// <inheritdoc/>
    public bool TryPop([MaybeNullWhen(false)] out CXXRecordDecl record)
    {
        return _decls.TryDequeue(out record);
    }
}