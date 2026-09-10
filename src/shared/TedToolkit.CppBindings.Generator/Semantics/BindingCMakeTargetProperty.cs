// -----------------------------------------------------------------------
// <copyright file="BindingCMakeTargetProperty.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one emitted CMake target property.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="Value">The property value expression.</param>
public sealed record BindingCMakeTargetProperty(string Name, string Value);