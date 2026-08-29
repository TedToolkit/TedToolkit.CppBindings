// -----------------------------------------------------------------------
// <copyright file="NativeObjectKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Models.Declarations;

/// <summary>
/// Identifies the generated ownership shape for a native object.
/// </summary>
internal enum NativeObjectKind
{
    /// <summary>
    /// The compiler probe has not completed.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A trivially copyable and trivially destructible value.
    /// </summary>
    Value = 1,

    /// <summary>
    /// A non-transient object requiring deterministic native destruction.
    /// </summary>
    Owned = 2,

    /// <summary>
    /// A Standard_Transient object using intrusive reference ownership.
    /// </summary>
    Handle = 3,
}