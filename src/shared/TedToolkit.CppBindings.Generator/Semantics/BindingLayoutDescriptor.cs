// -----------------------------------------------------------------------
// <copyright file="BindingLayoutDescriptor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes compiler-proved native layout facts.
/// </summary>
/// <param name="NativeTypeName">The canonical native type spelling.</param>
/// <param name="Size">The native size in bytes.</param>
/// <param name="Alignment">The native alignment in bytes.</param>
public sealed record BindingLayoutDescriptor(string NativeTypeName, long Size, long Alignment);