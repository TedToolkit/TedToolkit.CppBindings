// -----------------------------------------------------------------------
// <copyright file="BindingBufferOwnerOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a buffer-backed owner construction operation.
/// </summary>
/// <param name="MethodName">The public factory method.</param>
/// <param name="OwnerName">The constructed owner definition.</param>
/// <param name="OwnedResultName">The conditional owned result, or <see langword="null"/> for a direct owner.</param>
/// <param name="StatusName">The conditional status type, or <see langword="null"/> for a direct owner.</param>
/// <param name="SuccessMember">The status member denoting construction, or <see langword="null"/>.</param>
/// <param name="NativeReturnType">The native operation return type.</param>
/// <param name="NativeFailureExpression">The native fallback used by exception projection.</param>
/// <param name="Buffers">The ordered pointer/count transports.</param>
/// <param name="NativeExport">The native operation export.</param>
/// <param name="NativeBody">The provider-owned native algorithm body.</param>
public sealed record BindingBufferOwnerOperationDefinition(
    string MethodName,
    string OwnerName,
    string? OwnedResultName,
    string? StatusName,
    string? SuccessMember,
    string NativeReturnType,
    string NativeFailureExpression,
    IReadOnlyList<BindingBufferDefinition> Buffers,
    string NativeExport,
    string NativeBody) : BindingFiniteOperationDefinition(NativeExport, NativeBody);