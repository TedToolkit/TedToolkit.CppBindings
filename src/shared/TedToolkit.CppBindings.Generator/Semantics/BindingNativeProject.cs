// -----------------------------------------------------------------------
// <copyright file="BindingNativeProject.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes the provider-specific native build file emitted from Shared's complete source inventory.
/// </summary>
/// <param name="RelativePath">The output-relative native build-file path.</param>
/// <param name="RenderAsync">The renderer receiving every compiled native source path.</param>
public sealed record BindingNativeProject(
    string RelativePath,
    Func<IReadOnlyList<string>, TextWriter, CancellationToken, Task> RenderAsync);