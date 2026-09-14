// -----------------------------------------------------------------------
// <copyright file="BaseRelationModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Stores one direct native inheritance relationship and its pointer-conversion strategy.
/// </summary>
public sealed record BaseRelationModel
{
    /// <summary>
    /// Gets the direct base record.
    /// </summary>
    public required RecordModel Base { get; init; }

    /// <summary>
    /// Gets a value indicating whether the C++ base is virtual.
    /// </summary>
    public required bool IsVirtual { get; init; }

    /// <summary>
    /// Gets a value indicating whether consumers may convert to this base.
    /// </summary>
    public bool IsPublic { get; init; } = true;

    /// <summary>
    /// Gets the proved pointer-conversion strategy.
    /// </summary>
    public required PointerAdjustmentKind PointerAdjustment { get; init; }
}