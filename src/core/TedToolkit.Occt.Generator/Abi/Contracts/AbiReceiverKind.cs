// -----------------------------------------------------------------------
// <copyright file="AbiReceiverKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Abi.Contracts;

/// <summary>
/// Identifies the receiver contract of an ABI operation.
/// </summary>
internal enum AbiReceiverKind
{
    /// <summary>
    /// The operation has no receiver.
    /// </summary>
    None = 0,

    /// <summary>
    /// The operation borrows a const receiver.
    /// </summary>
    BorrowedConst = 1,

    /// <summary>
    /// The operation borrows a mutable receiver.
    /// </summary>
    BorrowedMutable = 2,
}