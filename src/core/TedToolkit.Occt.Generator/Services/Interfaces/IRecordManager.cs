// -----------------------------------------------------------------------
// <copyright file="IRecordManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using ClangSharp;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Tracks record declarations that still need to be generated.
/// </summary>
public interface IRecordManager
{
    /// <summary>
    /// Attempts to add a record declaration to the queue.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns><see langword="true"/> if the record was added; otherwise, <see langword="false"/>.</returns>
    bool Add(CXXRecordDecl record);

    /// <summary>
    /// Gets the records currently queued for generation.
    /// </summary>
    /// <returns>The queued records.</returns>
    IReadOnlyList<CXXRecordDecl> GetRecords();

    /// <summary>
    /// Removes the next queued record declaration, if any.
    /// </summary>
    /// <param name="record">The dequeued record declaration.</param>
    /// <returns><see langword="true"/> if a record was dequeued; otherwise, <see langword="false"/>.</returns>
    bool TryPop([MaybeNullWhen(false)] out CXXRecordDecl record);
}