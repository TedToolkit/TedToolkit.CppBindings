// -----------------------------------------------------------------------
// <copyright file="BindingCompositeFieldDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one native output and its managed result projection.
/// </summary>
/// <param name="NativeType">The native output type.</param>
/// <param name="ManagedType">The managed result type.</param>
/// <param name="Name">The result field name.</param>
/// <param name="InitialValue">The managed initialization expression.</param>
/// <param name="Projection">The managed expression that projects the native local.</param>
public sealed record BindingCompositeFieldDefinition(
    string NativeType,
    string ManagedType,
    string Name,
    string InitialValue,
    string Projection);