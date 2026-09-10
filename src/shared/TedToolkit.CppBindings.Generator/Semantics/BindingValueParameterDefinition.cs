// -----------------------------------------------------------------------
// <copyright file="BindingValueParameterDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one scalar or blittable-value parameter.
/// </summary>
/// <param name="Name">The managed parameter name.</param>
/// <param name="ManagedType">The managed parameter type.</param>
/// <param name="NativeType">The native parameter type.</param>
/// <param name="PassByPointer">Whether the native call transports a pointer to the value.</param>
/// <param name="RequireDefinedEnum">Whether managed validation requires a defined enum value.</param>
/// <param name="ManagedTransportType">The managed function-pointer ABI type, or the public type when omitted.</param>
/// <param name="ManagedArgumentExpression">The managed ABI projection with <c>$value</c> as the public value.</param>
public sealed record BindingValueParameterDefinition(
    string Name,
    string ManagedType,
    string NativeType,
    bool PassByPointer = false,
    bool RequireDefinedEnum = false,
    string? ManagedTransportType = null,
    string ManagedArgumentExpression = "$value");