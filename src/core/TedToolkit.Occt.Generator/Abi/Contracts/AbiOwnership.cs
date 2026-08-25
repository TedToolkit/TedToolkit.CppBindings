// -----------------------------------------------------------------------
// <copyright file="AbiOwnership.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Abi.Contracts;

/// <summary>
/// Identifies the ownership contract of an ABI value.
/// </summary>
internal enum AbiOwnership
{
    /// <summary>
    /// The value is copied by semantic value.
    /// </summary>
    Value = 0,

    /// <summary>
    /// The value is borrowed for the call.
    /// </summary>
    Borrowed = 1,

    /// <summary>
    /// The value transfers one ownership token.
    /// </summary>
    Owned = 2,
}