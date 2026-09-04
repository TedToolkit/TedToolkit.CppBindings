// -----------------------------------------------------------------------
// <copyright file="RecordModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

/// <summary>
/// Stores the projections of one parsed C++ type across native interop and generated C# surfaces.
/// </summary>
internal sealed class RecordModel
{
    /// <summary>
    /// Gets the mixed generic/fixed template projection, when this record belongs to a managed template family.
    /// </summary>
    public TemplateProjectionModel? TemplateProjection { get; set; }

    /// <summary>
    /// Gets a value indicating whether native callers can name this record.
    /// </summary>
    public bool IsPubliclyAccessible { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether another emitted record requires this complete type.
    /// </summary>
    public bool IsRequiredDependency { get; set; }

    /// <summary>
    /// Gets a value indicating whether every template type argument names a concrete native type.
    /// </summary>
    public bool IsClosedTemplateSpecialization { get; init; } = true;

    /// <summary>
    /// Gets the XML documentation description items for the record.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets or sets the direct base relationships.
    /// </summary>
    public IReadOnlyList<BaseRelationModel> Bases { get; set; } = [];

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
    /// Gets or sets the headers required to make the complete native declaration valid.
    /// </summary>
    public IReadOnlyList<string> NativeRequiredHeaders { get; set; } = [];

    /// <summary>
    /// Gets or sets the records whose complete declarations must precede this record's header.
    /// </summary>
    public IReadOnlyList<RecordModel> NativeDependencyRecords { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether construction must use the record's allocator placement operator.
    /// </summary>
    public bool UsesAllocatorPlacementNew { get; set; }

    /// <summary>
    /// Gets the projected type metadata.
    /// </summary>
    public required TypeModel Type { get; init; }

    /// <summary>
    /// Gets the native record size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Gets or sets the compiler-proved native alignment in bytes.
    /// </summary>
    public long Alignment { get; set; }

    /// <summary>
    /// Gets or sets the compiler-proved ownership shape.
    /// </summary>
    public NativeObjectKind ObjectKind { get; set; }

    /// <summary>
    /// Gets or sets the projected fields.
    /// </summary>
    public IReadOnlyList<FieldModel> FieldModels { get; set; } = null!;

    /// <summary>
    /// Gets or sets the projected methods.
    /// </summary>
    public IReadOnlyList<MethodModel> MethodModels { get; set; } = null!;
}