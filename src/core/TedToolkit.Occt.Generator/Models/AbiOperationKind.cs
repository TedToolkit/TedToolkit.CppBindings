// -----------------------------------------------------------------------
// <copyright file="AbiOperationKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Identifies an ABI operation category.
/// </summary>
internal enum AbiOperationKind
{
    /// <summary>
    /// A constructor operation.
    /// </summary>
    Constructor = 0,

    /// <summary>
    /// A named method operation.
    /// </summary>
    Method = 1,

    /// <summary>
    /// An operator operation.
    /// </summary>
    Operator = 2,

    /// <summary>
    /// A conversion operation.
    /// </summary>
    Conversion = 3,

    /// <summary>
    /// An ordinary object destruction operation.
    /// </summary>
    Destroy = 4,

    /// <summary>
    /// A transient ownership-retain operation.
    /// </summary>
    Retain = 5,

    /// <summary>
    /// A transient ownership-release operation.
    /// </summary>
    Release = 6,
}