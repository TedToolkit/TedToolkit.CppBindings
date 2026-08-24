// -----------------------------------------------------------------------
// <copyright file="AbiOperationModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores the semantic identity and projections of one candidate ABI operation.
/// </summary>
internal sealed class AbiOperationModel
{
    /// <summary>
    /// Gets the approved semantic owner identifier.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the approved semantic operation identifier.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// Gets the operation category.
    /// </summary>
    public required AbiOperationKind Kind { get; init; }

    /// <summary>
    /// Gets the receiver contract.
    /// </summary>
    public required AbiReceiverKind Receiver { get; init; }

    /// <summary>
    /// Gets the source declaration used in diagnostics only.
    /// </summary>
    public required string Declaration { get; init; }

    /// <summary>
    /// Gets the source location used in diagnostics only.
    /// </summary>
    public required string SourceLocation { get; init; }

    /// <summary>
    /// Gets the ordered parameter contracts.
    /// </summary>
    public required IReadOnlyList<AbiParameterModel> Parameters { get; init; }

    /// <summary>
    /// Gets the result contract, or <see langword="null"/> for no result payload.
    /// </summary>
    public AbiResultModel? Result { get; init; }
}