// -----------------------------------------------------------------------
// <copyright file="AbiNullability.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Identifies whether an ABI value may be null.
/// </summary>
internal enum AbiNullability
{
    /// <summary>
    /// The value is required.
    /// </summary>
    Required = 0,

    /// <summary>
    /// The value may be null.
    /// </summary>
    Nullable = 1,
}