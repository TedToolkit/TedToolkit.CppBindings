// -----------------------------------------------------------------------
// <copyright file="BindingOwnedFactoryDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a buffer-backed factory whose status controls owner construction.
/// </summary>
/// <param name="MethodName">The public factory method name.</param>
/// <param name="NativeExport">The native factory export.</param>
/// <param name="SuccessMember">The status member that denotes construction.</param>
/// <param name="NativeFailureExpression">The native status expression returned after exception projection.</param>
/// <param name="Buffers">The ordered pointer and length buffer pairs.</param>
public sealed record BindingOwnedFactoryDefinition(
    string MethodName,
    string NativeExport,
    string SuccessMember,
    string NativeFailureExpression,
    IReadOnlyList<BindingBufferDefinition> Buffers);