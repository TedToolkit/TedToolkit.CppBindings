// -----------------------------------------------------------------------
// <copyright file="BindingFiniteOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Identifies one composable finite-profile operation.
/// </summary>
/// <param name="NativeExport">The operation's native export.</param>
/// <param name="NativeBody">The provider-owned native algorithm body.</param>
public abstract record BindingFiniteOperationDefinition(string NativeExport, string NativeBody);