// -----------------------------------------------------------------------
// <copyright file="OcctErrorKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt;

/// <summary>
/// Represents the stable category reported by a failed native OCCT operation.
/// </summary>
/// <remarks>
/// Undefined nonzero values remain failures and are preserved when exposed through
/// <see cref="IOcctException.ErrorKind"/>.
/// </remarks>
public enum OcctErrorKind
{
    /// <summary>
    /// Indicates that the native operation completed successfully.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that a native argument was invalid.
    /// </summary>
    Argument = 1,

    /// <summary>
    /// Indicates that a native argument was outside its accepted range.
    /// </summary>
    ArgumentOutOfRange = 2,

    /// <summary>
    /// Indicates that a native arithmetic operation failed.
    /// </summary>
    Arithmetic = 3,

    /// <summary>
    /// Indicates that the native operation was invalid for the current object state.
    /// </summary>
    InvalidOperation = 4,

    /// <summary>
    /// Indicates that a required native object was null.
    /// </summary>
    NullObject = 5,

    /// <summary>
    /// Indicates that the native operation could not allocate memory.
    /// </summary>
    OutOfMemory = 6,

    /// <summary>
    /// Indicates that a native arithmetic conversion or operation overflowed.
    /// </summary>
    Overflow = 7,

    /// <summary>
    /// Indicates that OCCT reported a <c>Standard_Failure</c>.
    /// </summary>
    OcctFailure = 8,

    /// <summary>
    /// Indicates that native code reported a standard C++ exception.
    /// </summary>
    StandardException = 9,

    /// <summary>
    /// Indicates that native code reported a failure of unknown origin.
    /// </summary>
    Unknown = 255,
}