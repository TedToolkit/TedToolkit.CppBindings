// -----------------------------------------------------------------------
// <copyright file="CAbiHeaderGenerationResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Contains the canonical C header and deterministic diagnostics from one generation run.
/// </summary>
internal sealed class CAbiHeaderGenerationResult
{
    /// <summary>
    /// Gets the canonical ABI-major-1 C11 header.
    /// </summary>
    public required string Header { get; init; }

    /// <summary>
    /// Gets the diagnostics for members omitted before naming and emission.
    /// </summary>
    public required IReadOnlyList<AbiProjectionDiagnostic> Diagnostics { get; init; }
}