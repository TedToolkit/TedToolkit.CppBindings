// -----------------------------------------------------------------------
// <copyright file="CAbiGenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.CAbi.Generator;

/// <summary>
/// Configures generation for selected external C linkage declarations.
/// </summary>
public sealed record CAbiGenerationOptions : GenerationOptions
{
    /// <summary>
    /// Gets the headers whose own declarations are eligible for projection.
    /// </summary>
    public required IReadOnlyList<CAbiHeader> Headers { get; init; }

    /// <summary>
    /// Gets the include directories used for discovery and native adapter compilation.
    /// </summary>
    public IReadOnlyList<DirectoryPath> IncludeDirectories { get; init; } = [];

    /// <summary>
    /// Gets the preprocessor definitions used for discovery and native adapter compilation.
    /// </summary>
    public IReadOnlyList<string> CompileDefinitions { get; init; } = [];

    /// <summary>
    /// Gets the libraries or import libraries linked into the generated native adapter.
    /// </summary>
    public IReadOnlyList<string> LinkLibraries { get; init; } = [];
}