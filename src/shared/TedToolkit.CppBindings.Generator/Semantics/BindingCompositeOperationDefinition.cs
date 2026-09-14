// -----------------------------------------------------------------------
// <copyright file="BindingCompositeOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes an operation that projects native outputs into a composite result.
/// </summary>
/// <param name="ContainingType">The managed static containing type.</param>
/// <param name="MethodName">The managed method name.</param>
/// <param name="ResultName">The referenced composite result.</param>
/// <param name="Owners">The ordered owner parameters.</param>
/// <param name="Values">The ordered scalar and value parameters.</param>
/// <param name="NativeExport">The native operation export.</param>
/// <param name="NativeBody">The provider-owned native algorithm body.</param>
public sealed record BindingCompositeOperationDefinition(
    string ContainingType,
    string MethodName,
    string ResultName,
    IReadOnlyList<BindingOwnerParameterDefinition> Owners,
    IReadOnlyList<BindingValueParameterDefinition> Values,
    string NativeExport,
    string NativeBody) : BindingFiniteOperationDefinition(NativeExport, NativeBody);