// -----------------------------------------------------------------------
// <copyright file="IRecordLayoutService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using ModularPipelines.Context.Domains;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides native C++ layout data for parsed records.
/// </summary>
public interface IRecordLayoutService
{
    /// <summary>
    /// Builds and runs the native layout probe for the supplied records.
    /// </summary>
    /// <param name="records">The records to inspect.</param>
    /// <param name="shell">The module shell used to run external tools.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when layout data is cached.</returns>
    Task PrepareAsync(IEnumerable<CXXRecordDecl> records, IShellContext shell, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a prepared record size.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The record size in bytes.</returns>
    long GetSize(CXXRecordDecl record);

    /// <summary>
    /// Gets a prepared field offset in the given record.
    /// </summary>
    /// <param name="record">The containing record declaration.</param>
    /// <param name="field">The field declaration.</param>
    /// <returns>The field offset in bytes.</returns>
    long GetOffset(CXXRecordDecl record, FieldDecl field);
}