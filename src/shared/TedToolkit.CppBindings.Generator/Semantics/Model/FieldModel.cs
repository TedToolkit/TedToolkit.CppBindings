// -----------------------------------------------------------------------
// <copyright file="FieldModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Stores the projected metadata for one field declaration.
/// </summary>
public sealed class FieldModel
{
    /// <summary>
    /// Gets or sets whether cyclic handle storage requires an in-place reference property.
    /// </summary>
    public bool UsesIntrusiveHandleReferenceStorage { get; set; }

    /// <summary>
    /// Gets the native bit width, or null for an ordinary field. Zero-width fields only affect layout.
    /// </summary>
    public int? BitWidth { get; init; }

    /// <summary>
    /// Gets the bit offset within the native allocation unit identified by Offset and Size.
    /// </summary>
    public int BitOffset { get; init; }

    /// <summary>
    /// Gets a value indicating whether bitfield reads require sign extension.
    /// </summary>
    public bool IsSignedBitField { get; init; }

    /// <summary>
    /// Gets a value indicating whether the native bitfield is const-qualified.
    /// </summary>
    public bool IsReadOnlyBitField { get; init; }

    /// <summary>
    /// Gets the open managed field type when the declaring record is a generic template family.
    /// </summary>
    public string CSharpTemplateType { get; init; } = "";

    /// <summary>
    /// Gets the native dependent field type spelling from the template declaration.
    /// </summary>
    public string CppTemplateType { get; init; } = "";

    /// <summary>
    /// Gets the XML documentation description items for the field.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the native field or bitfield allocation-unit offset in bytes.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// Gets the native field size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Gets the native field alignment in bytes.
    /// </summary>
    public required long Alignment { get; init; }

    /// <summary>
    /// Gets the generated field name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the projected field type.
    /// </summary>
    public required TypeModel Type { get; init; }
}