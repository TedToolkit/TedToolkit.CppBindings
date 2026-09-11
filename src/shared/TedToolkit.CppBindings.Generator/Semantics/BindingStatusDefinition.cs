// -----------------------------------------------------------------------
// <copyright file="BindingStatusDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a projected native status enumeration.
/// </summary>
/// <param name="Name">The managed enumeration name.</param>
/// <param name="Members">The ordered member names and values.</param>
public sealed record BindingStatusDefinition(string Name, IReadOnlyList<KeyValuePair<string, int>> Members);