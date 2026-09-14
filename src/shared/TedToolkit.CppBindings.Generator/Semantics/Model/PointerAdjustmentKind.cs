// -----------------------------------------------------------------------
// <copyright file="PointerAdjustmentKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes how a derived native pointer becomes one of its direct base pointers.
/// </summary>
public enum PointerAdjustmentKind
{
    /// <summary>
    /// The derived and base subobject addresses are identical, so managed code can reinterpret the pointer.
    /// </summary>
    Identity = 0,

    /// <summary>
    /// Generated C++ must perform the pointer conversion.
    /// </summary>
    NativeAdjust = 1,
}