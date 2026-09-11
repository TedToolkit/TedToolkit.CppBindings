// -----------------------------------------------------------------------
// <copyright file="BindingBufferDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one managed span projected as a native pointer and element count.
/// </summary>
/// <param name="Name">The managed span parameter name.</param>
/// <param name="ElementType">The managed element type.</param>
/// <param name="NativeElementType">The native element type.</param>
/// <param name="ElementsPerItem">The required element grouping.</param>
/// <param name="LengthError">The validation message for an incomplete group.</param>
/// <param name="RequiresIndicesBelowFirstBufferItemCount">Whether every value indexes the first buffer's grouped items.</param>
/// <param name="IndexError">The validation message for an out-of-range index.</param>
/// <param name="PointerName">The managed fixed-pointer local name, or a name derived from <paramref name="Name"/>.</param>
public sealed record BindingBufferDefinition(
    string Name,
    string ElementType,
    string NativeElementType,
    int ElementsPerItem,
    string LengthError,
    bool RequiresIndicesBelowFirstBufferItemCount = false,
    string IndexError = "An index must be less than the item count.",
    string? PointerName = null);