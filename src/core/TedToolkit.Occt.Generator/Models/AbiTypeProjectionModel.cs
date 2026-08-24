// -----------------------------------------------------------------------
// <copyright file="AbiTypeProjectionModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores one type's explicit source, transport, adapter, and managed projections.
/// </summary>
internal sealed class AbiTypeProjectionModel
{
    /// <summary>
    /// Gets the source C++ spelling used only inside the native adapter.
    /// </summary>
    public required string SourceCppTypeName { get; init; }

    /// <summary>
    /// Gets the versioned semantic ABI transport identifier.
    /// </summary>
    public string? TransportId { get; init; }

    /// <summary>
    /// Gets the C11 type spelling used at the ABI boundary.
    /// </summary>
    public string? CAbiTypeName { get; init; }

    /// <summary>
    /// Gets the C++ type spelling used by the explicit adapter conversion.
    /// </summary>
    public string? CppAdapterTypeName { get; init; }

    /// <summary>
    /// Gets the managed transport type used by P/Invoke.
    /// </summary>
    public DataType? ManagedTransportType { get; init; }

    /// <summary>
    /// Gets the public managed projection type.
    /// </summary>
    public DataType? PublicManagedType { get; init; }
}