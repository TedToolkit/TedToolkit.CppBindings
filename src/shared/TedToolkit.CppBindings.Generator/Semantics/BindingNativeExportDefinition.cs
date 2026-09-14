// -----------------------------------------------------------------------
// <copyright file="BindingNativeExportDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one additional provider-native export with a complete signature and body.
/// </summary>
/// <param name="Name">The export name.</param>
/// <param name="ReturnType">The native return type.</param>
/// <param name="Parameters">The native parameter list without parentheses.</param>
/// <param name="Body">The native function body without braces.</param>
public sealed record BindingNativeExportDefinition(
    string Name,
    string ReturnType,
    string Parameters,
    string Body);