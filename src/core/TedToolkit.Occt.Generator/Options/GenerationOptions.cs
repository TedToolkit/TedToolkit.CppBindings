// -----------------------------------------------------------------------
// <copyright file="GenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;

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
    public Func<FieldDecl, bool> FieldTypeToGenerate { get; init; } = _ => true;

    /// <summary>
    /// Gets or sets the explicit vcpkg triplet to use. When empty, the default resolver is used.
    /// </summary>
    public string Triplet { get; init; } = "";

    /// <summary>
    /// Gets or sets the C++ language standard version passed to clang and CMake.
    /// </summary>
    public int CppVersion { get; set; } = 17;

    /// <summary>
    /// Gets the active vcpkg triplet, falling back to the default resolver when necessary.
    /// </summary>
    /// <param name="defaultsResolver">The default triplet resolver.</param>
    /// <returns>The active triplet.</returns>
    internal string GetTriplet(IVcpkgDefaultTripletResolver defaultsResolver)
    {
        ArgumentNullException.ThrowIfNull(defaultsResolver);
        return string.IsNullOrWhiteSpace(Triplet) ? defaultsResolver.GetTriplet() : Triplet;
    }
}