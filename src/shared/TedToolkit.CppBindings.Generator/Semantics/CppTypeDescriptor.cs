// -----------------------------------------------------------------------
// <copyright file="CppTypeDescriptor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a provider-independent C++ type before projection.
/// </summary>
/// <param name="NativeName">The canonical native type spelling.</param>
/// <param name="IsConst">Whether the terminal native value is const-qualified.</param>
/// <param name="PointerDepth">The number of pointer layers surrounding the terminal value.</param>
public sealed record CppTypeDescriptor(string NativeName, bool IsConst = false, int PointerDepth = 0);