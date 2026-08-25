// -----------------------------------------------------------------------
// <copyright file="AbiParameterModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Abi.Contracts;

/// <summary>
/// Stores one explicit ABI parameter contract.
/// </summary>
internal sealed class AbiParameterModel
{
    /// <summary>
    /// Gets the semantic parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the value direction.
    /// </summary>
    public required AbiDirection Direction { get; init; }

    /// <summary>
    /// Gets the nullability contract.
    /// </summary>
    public required AbiNullability Nullability { get; init; }

    /// <summary>
    /// Gets the ownership contract.
    /// </summary>
    public required AbiOwnership Ownership { get; init; }

    /// <summary>
    /// Gets the explicit type projections.
    /// </summary>
    public required AbiTypeProjectionModel Type { get; init; }
}