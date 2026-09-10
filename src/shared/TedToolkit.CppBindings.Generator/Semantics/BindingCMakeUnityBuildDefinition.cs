// -----------------------------------------------------------------------
// <copyright file="BindingCMakeUnityBuildDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes deterministic family/depth unity grouping.
/// </summary>
public sealed record BindingCMakeUnityBuildDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingCMakeUnityBuildDefinition"/> record.
    /// </summary>
    /// <param name="batchSize">The maximum number of source files per unity group.</param>
    public BindingCMakeUnityBuildDefinition(int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        BatchSize = batchSize;
    }

    /// <summary>
    /// Gets the maximum number of source files per unity group.
    /// </summary>
    public int BatchSize { get; }
}