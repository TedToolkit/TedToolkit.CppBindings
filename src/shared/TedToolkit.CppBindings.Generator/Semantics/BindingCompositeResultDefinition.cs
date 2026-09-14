// -----------------------------------------------------------------------
// <copyright file="BindingCompositeResultDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a managed result assembled from native output values.
/// </summary>
/// <param name="Name">The managed result type.</param>
/// <param name="Fields">The ordered result fields.</param>
/// <param name="Kind">The managed result representation.</param>
/// <param name="ComputedProperties">Additional expression-bodied managed properties.</param>
public sealed record BindingCompositeResultDefinition(
    string Name,
    IReadOnlyList<BindingCompositeFieldDefinition> Fields,
    BindingCompositeResultKind Kind = BindingCompositeResultKind.RecordStruct,
    IReadOnlyList<BindingComputedPropertyDefinition>? ComputedProperties = null);