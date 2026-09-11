// -----------------------------------------------------------------------
// <copyright file="BindingCMakePackageDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one CMake package requirement.
/// </summary>
/// <param name="Name">The package name.</param>
/// <param name="Arguments">The arguments following the package name.</param>
public sealed record BindingCMakePackageDefinition(string Name, string Arguments);