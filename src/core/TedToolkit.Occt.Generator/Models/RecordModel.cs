// -----------------------------------------------------------------------
// <copyright file="RecordModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the projections of one parsed C++ type across native interop and generated C# surfaces.
/// </summary>
internal sealed class RecordModel
{
    /// <summary>
    /// Gets the XML documentation description items for the record.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets or sets the projected base record, when one exists.
    /// </summary>
    public RecordModel? Base { get; set; }

    /// <summary>
    /// Gets a value indicating whether the record is abstract.
    /// </summary>
    public required bool IsAbstract { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the record derives from <c>Standard_Transient</c>.
    /// </summary>
    public required bool IsStandardTransient { get; set; }

    /// <summary>
    /// Gets the native header file that declares the record.
    /// </summary>
    public required string SourceHeader { get; init; }

    /// <summary>
    /// Gets the projected type metadata.
    /// </summary>
    public required TypeModel Type { get; init; }

    /// <summary>
    /// Gets the native record size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Gets or sets the projected fields.
    /// </summary>
    public IReadOnlyList<FieldModel> FieldModels { get; set; } = null!;

    /// <summary>
    /// Gets or sets the projected methods.
    /// </summary>
    public IReadOnlyList<MethodModel> MethodModels { get; set; } = null!;
}