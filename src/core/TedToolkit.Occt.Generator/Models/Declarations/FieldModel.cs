// -----------------------------------------------------------------------
// <copyright file="FieldModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models.Declarations;

/// <summary>
/// Stores the projected metadata for one field declaration.
/// </summary>
internal sealed class FieldModel
{
    /// <summary>
    /// Gets the XML documentation description items for the field.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the native field offset in bytes.
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