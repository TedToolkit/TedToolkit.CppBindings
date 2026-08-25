// -----------------------------------------------------------------------
// <copyright file="EnumModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Models.Declarations;

/// <summary>
/// Stores the projected metadata for one enum declaration.
/// </summary>
internal sealed class EnumModel
{
    /// <summary>
    /// Gets the XML documentation description items for the enum.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the generated enum type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the original native enum type name.
    /// </summary>
    public required string SourceType { get; init; }

    /// <summary>
    /// Gets the generated underlying enum type.
    /// </summary>
    public required DataType UnderlyingType { get; init; }

    /// <summary>
    /// Gets the generated enum members.
    /// </summary>
    public required IReadOnlyList<EnumMemberModel> Members { get; init; }
}