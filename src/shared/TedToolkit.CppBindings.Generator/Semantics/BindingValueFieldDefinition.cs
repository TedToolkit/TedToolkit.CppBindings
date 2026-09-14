// -----------------------------------------------------------------------
// <copyright file="BindingValueFieldDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one field of a blittable value type.
/// </summary>
/// <param name="Type">The managed and native scalar type.</param>
/// <param name="Name">The public property name.</param>
public sealed record BindingValueFieldDefinition(string Type, string Name);