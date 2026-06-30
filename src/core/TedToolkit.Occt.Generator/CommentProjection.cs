// -----------------------------------------------------------------------
// <copyright file="CommentProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator;

/// <summary>
/// Stores the projected XML documentation parts extracted from a Clang comment tree.
/// </summary>
internal sealed class CommentProjection
{
    /// <summary>
    /// Gets the empty comment projection.
    /// </summary>
    public static CommentProjection Empty { get; } = new();

    /// <summary>
    /// Gets the root description items for the owning symbol.
    /// </summary>
    public IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; } = [];

    /// <summary>
    /// Gets the parameter descriptions keyed by parameter name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IDescriptionItem>> ParameterDescriptionItems { get; init; } =
        new ReadOnlyDictionary<string, IReadOnlyList<IDescriptionItem>>(
            new Dictionary<string, IReadOnlyList<IDescriptionItem>>(StringComparer.Ordinal));

    /// <summary>
    /// Gets the return value description items.
    /// </summary>
    public IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; } = [];
}