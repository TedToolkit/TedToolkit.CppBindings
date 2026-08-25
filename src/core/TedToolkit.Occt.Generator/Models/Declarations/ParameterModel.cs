// -----------------------------------------------------------------------
// <copyright file="ParameterModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models.Declarations;

/// <summary>
/// Stores the projected metadata for one method parameter.
/// </summary>
internal class ParameterModel
{
    /// <summary>
    /// Gets the XML documentation description items for the parameter.
    /// </summary>
    public required IReadOnlyList<IDescriptionItem> DescriptionItems { get; init; }

    /// <summary>
    /// Gets the projected parameter type.
    /// </summary>
    public required TypeModel Type { get; init; }

    /// <summary>
    /// Gets the generated parameter name.
    /// </summary>
    public required string Name { get; init; }
}