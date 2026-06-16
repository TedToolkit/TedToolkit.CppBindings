// -----------------------------------------------------------------------
// <copyright file="RecordService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Provides record metadata, including inherited fields and methods.
/// </summary>
public sealed class RecordService : IRecordService
{
    /// <inheritdoc/>
    public string GetName(CXXRecordDecl decl)
    {
        ArgumentNullException.ThrowIfNull(decl);

        return decl.Name;
    }

    /// <inheritdoc/>
    public ClangSharp.Type GetType(CXXRecordDecl decl)
    {
        ArgumentNullException.ThrowIfNull(decl);

        return decl.TypeForDecl;
    }

    /// <inheritdoc/>
    public IEnumerable<FieldDecl> GetFields(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return GetFieldsCore(record);
    }

    private IEnumerable<FieldDecl> GetFieldsCore(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var fieldDecl in GetFields(baseDecl))
            {
                yield return fieldDecl;
            }
        }

        foreach (var recordField in record.Fields)
        {
            yield return recordField;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<CXXMethodDecl> GetMethods(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return GetMethodsCore(record);
    }

    private IEnumerable<CXXMethodDecl> GetMethodsCore(CXXRecordDecl record)
    {
        foreach (var cxxBaseSpecifier in record.Bases)
        {
            if (cxxBaseSpecifier.Type.AsCXXRecordDecl is not { } baseDecl)
            {
                continue;
            }

            foreach (var methodDecl in GetMethods(baseDecl))
            {
                yield return methodDecl;
            }
        }

        foreach (var recordMethod in record.Methods)
        {
            yield return recordMethod;
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetSizeAsync(CXXRecordDecl record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var type = record.TypeForDecl.Handle;
        type = clang.getCanonicalType(type);
        var size = clang.Type_getSizeOf(type);

        if (size < 0)
        {
            throw new InvalidOperationException($"Cannot get sizeof record ({GetName(record)}): {size}");
        }

        return size;
    }
}