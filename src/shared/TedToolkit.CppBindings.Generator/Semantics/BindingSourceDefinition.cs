// -----------------------------------------------------------------------
// <copyright file="BindingSourceDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one provider-specific supplemental source before Shared constructs the generation plan.
/// </summary>
/// <param name="RelativePath">The output-relative source path.</param>
/// <param name="RenderAsync">The cancellation-aware source renderer.</param>
public sealed record BindingSourceDefinition(
    string RelativePath,
    Func<TextWriter, CancellationToken, Task> RenderAsync);