// -----------------------------------------------------------------------
// <copyright file="GeneratedSource.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Describes one output-relative source and its cancellation-aware streaming renderer.
/// </summary>
/// <param name="RelativePath">The source path beneath its configured language output root.</param>
/// <param name="RenderAsync">The renderer; it must not dispose or retain the supplied writer.</param>
public sealed record GeneratedSource(
    string RelativePath,
    Func<TextWriter, CancellationToken, Task> RenderAsync);