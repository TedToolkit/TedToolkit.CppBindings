// -----------------------------------------------------------------------
// <copyright file="IRecordModelManager.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Models.Declarations;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Stores and exposes the record and enum models discovered during parsing.
/// </summary>
internal interface IRecordModelManager
{
    /// <summary>
    /// Adds a record declaration to the model cache and returns its projection.
    /// </summary>
    /// <param name="record">The record declaration to add.</param>
    /// <returns>The projected record model.</returns>
    RecordModel Add(CXXRecordDecl record);

    /// <summary>
    /// Gets the projected enum models discovered during parsing.
    /// </summary>
    IReadOnlyList<EnumModel> EnumModels { get; }

    /// <summary>
    /// Gets the projected record models discovered during parsing.
    /// </summary>
    IEnumerable<RecordModel> RecordModels { get; }
}