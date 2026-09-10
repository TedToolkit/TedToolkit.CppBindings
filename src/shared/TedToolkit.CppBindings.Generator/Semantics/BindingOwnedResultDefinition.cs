// -----------------------------------------------------------------------
// <copyright file="BindingOwnedResultDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a status plus conditionally constructed owned result.
/// </summary>
/// <param name="Name">The managed result type.</param>
/// <param name="StatusProperty">The status property name.</param>
/// <param name="OwnerProperty">The optional owner property name.</param>
public sealed record BindingOwnedResultDefinition(string Name, string StatusProperty, string OwnerProperty);