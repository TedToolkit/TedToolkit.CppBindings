// -----------------------------------------------------------------------
// <copyright file="AbiDirection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Identifies the direction of an ABI value.
/// </summary>
internal enum AbiDirection
{
    /// <summary>
    /// An input value.
    /// </summary>
    In = 0,

    /// <summary>
    /// An output value.
    /// </summary>
    Out = 1,

    /// <summary>
    /// An input and output value.
    /// </summary>
    InOut = 2,
}