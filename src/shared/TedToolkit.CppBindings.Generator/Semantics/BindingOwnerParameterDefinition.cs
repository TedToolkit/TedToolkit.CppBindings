// -----------------------------------------------------------------------
// <copyright file="BindingOwnerParameterDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one owner parameter.
/// </summary>
/// <param name="Name">The managed parameter name.</param>
/// <param name="OwnerName">The referenced owner definition.</param>
/// <param name="IsExtensionReceiver">Whether the parameter is the extension receiver.</param>
public sealed record BindingOwnerParameterDefinition(
    string Name,
    string OwnerName,
    bool IsExtensionReceiver = false);