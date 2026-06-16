// -----------------------------------------------------------------------
// <copyright file="GenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Options;

/// <summary>
/// Configures how OCCT source files are discovered and generated.
/// </summary>
public sealed record GenerationOptions()
{
    /// <summary>
    /// Gets a value indicating whether field offsets should be computed by running code.
    /// </summary>
    public bool GetFieldOffsetByRunning { get; init; }

    /// <summary>
    /// Gets the declarations to generate.
    /// </summary>
    public required IReadOnlyList<DeclOptions> DeclOptions { get; init; }

    /// <summary>
    /// Gets the extra command-line arguments passed to clang.
    /// </summary>
    public IReadOnlyList<string> CommandLineArgs { get; init; } = [];

    /// <summary>
    /// Gets the output folder for generated C# sources.
    /// </summary>
    public required DirectoryInfo CSharpFolder { get; init; }

    /// <summary>
    /// Gets the output folder for generated C++ sources.
    /// </summary>
    public required DirectoryInfo CppFolder { get; init; }

    /// <summary>
    /// Gets a value indicating whether generated types should be internal.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// Gets a predicate that skips matching clang types during generation.
    /// </summary>
    public Predicate<ClangSharp.Type> ShouldSkip { get; init; } = _ => false;
}