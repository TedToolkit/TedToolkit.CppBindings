// -----------------------------------------------------------------------
// <copyright file="EnumMemberModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the projected metadata for one enum member.
/// </summary>
internal sealed class EnumMemberModel
{
    /// <summary>
    /// Gets the XML documentation description items for the enum member.
    /// </summary>
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the generated enum member name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the generated enum member value expression.
    /// </summary>
    public required IExpression Value { get; init; }
}