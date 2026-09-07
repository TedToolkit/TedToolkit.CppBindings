// -----------------------------------------------------------------------
// <copyright file="BindingTypeProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes the native and managed spellings selected by a type rule.
/// </summary>
/// <param name="NativeTypeName">The type used by the native wrapper.</param>
/// <param name="ManagedTypeName">The type exposed by the managed binding.</param>
public sealed record BindingTypeProjection(string NativeTypeName, string ManagedTypeName);