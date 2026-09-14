// -----------------------------------------------------------------------
// <copyright file="BindingOwnerDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one native object stored inside <c>Owned&lt;T&gt;</c>.
/// </summary>
/// <param name="Name">The managed owner value type.</param>
/// <param name="NativeName">The native adapter type.</param>
/// <param name="Size">The proved native size.</param>
/// <param name="Alignment">The proved native alignment.</param>
/// <param name="DestroyExport">The native destruction export.</param>
public sealed record BindingOwnerDefinition(
    string Name,
    string NativeName,
    int Size,
    int Alignment,
    string DestroyExport);