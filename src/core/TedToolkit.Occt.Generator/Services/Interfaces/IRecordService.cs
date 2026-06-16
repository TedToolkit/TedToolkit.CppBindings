// -----------------------------------------------------------------------
// <copyright file="IRecordService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides metadata access for record declarations.
/// </summary>
public interface IRecordService : IDeclService<CXXRecordDecl>
{
    /// <summary>
    /// Gets the fields declared by the record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The record fields.</returns>
    IEnumerable<FieldDecl> GetFields(CXXRecordDecl record);

    /// <summary>
    /// Gets the methods declared by the record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The record methods.</returns>
    IEnumerable<CXXMethodDecl> GetMethods(CXXRecordDecl record);

    /// <summary>
    /// Gets the record size in bytes.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The record size.</returns>
    Task<long> GetSizeAsync(CXXRecordDecl record);
}