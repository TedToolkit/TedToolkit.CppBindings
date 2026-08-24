// -----------------------------------------------------------------------
// <copyright file="AbiContractValidationResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Contains the exportability outcome for one ABI operation.
/// </summary>
internal sealed class AbiContractValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the operation has every required explicit projection.
    /// </summary>
    public required bool IsExportable { get; init; }

    /// <summary>
    /// Gets the single deterministic rejection diagnostic, when the operation is not exportable.
    /// </summary>
    public AbiProjectionDiagnostic? Diagnostic { get; init; }
}