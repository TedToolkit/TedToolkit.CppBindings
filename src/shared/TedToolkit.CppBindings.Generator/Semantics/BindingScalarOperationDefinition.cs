// -----------------------------------------------------------------------
// <copyright file="BindingScalarOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes an owner operation that returns one scalar or enum value.
/// </summary>
/// <param name="ContainingType">The managed static containing type.</param>
/// <param name="MethodName">The managed method name.</param>
/// <param name="ManagedReturnType">The managed result type.</param>
/// <param name="NativeReturnType">The native result type.</param>
/// <param name="NativeFailureExpression">The native fallback used by exception projection.</param>
/// <param name="Owners">The ordered owner parameters.</param>
/// <param name="Values">The ordered scalar and value parameters.</param>
/// <param name="NativeExport">The native operation export.</param>
/// <param name="NativeBody">The provider-owned native algorithm body.</param>
public sealed record BindingScalarOperationDefinition(
    string ContainingType,
    string MethodName,
    string ManagedReturnType,
    string NativeReturnType,
    string NativeFailureExpression,
    IReadOnlyList<BindingOwnerParameterDefinition> Owners,
    IReadOnlyList<BindingValueParameterDefinition> Values,
    string NativeExport,
    string NativeBody) : BindingFiniteOperationDefinition(NativeExport, NativeBody);