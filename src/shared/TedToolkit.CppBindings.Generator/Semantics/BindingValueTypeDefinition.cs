// -----------------------------------------------------------------------
// <copyright file="BindingValueTypeDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one blittable value type.
/// </summary>
/// <param name="Name">The managed type name.</param>
/// <param name="NativeName">The native transport type name.</param>
/// <param name="Size">The proved native size.</param>
/// <param name="Alignment">The proved native alignment.</param>
/// <param name="Fields">The ordered value fields.</param>
public sealed record BindingValueTypeDefinition(
    string Name,
    string NativeName,
    int Size,
    int Alignment,
    IReadOnlyList<BindingValueFieldDefinition> Fields);