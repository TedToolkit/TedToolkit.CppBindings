// -----------------------------------------------------------------------
// <copyright file="BindingTwoPhaseBufferDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one array allocated between the count and copy phases.
/// </summary>
/// <param name="PropertyName">The composite-result property receiving the array.</param>
/// <param name="ElementType">The managed array element type.</param>
/// <param name="NativeElementType">The native buffer element type.</param>
/// <param name="CountName">The managed and native element-count name.</param>
/// <param name="PointerName">The managed/native pointer local name, or a name derived from <paramref name="PropertyName"/>.</param>
public sealed record BindingTwoPhaseBufferDefinition(
    string PropertyName,
    string ElementType,
    string NativeElementType,
    string CountName,
    string? PointerName = null);