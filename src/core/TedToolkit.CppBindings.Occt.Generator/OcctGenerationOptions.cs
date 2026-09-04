// -----------------------------------------------------------------------
// <copyright file="OcctGenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;

namespace TedToolkit.CppBindings.Occt.Generator;

/// <summary>
/// Adds OCCT declaration discovery and classification to generic generation options.
/// </summary>
public sealed record OcctGenerationOptions : GenerationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OcctGenerationOptions"/> class.
    /// </summary>
    public OcctGenerationOptions()
    {
        NativeLibraryBaseName = "ted_toolkit_occt";
        CSharpNamespace = "TedToolkit.CppBindings.Occt";
    }

    /// <summary>
    /// Gets a value indicating whether field offsets should be computed by running code.
    /// </summary>
    public bool GetFieldOffsetByRunning { get; init; }

    /// <summary>
    /// Gets the selected OCCT declarations.
    /// </summary>
    public required IReadOnlyList<OcctDeclarationOptions> DeclOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether all supported public OCCT headers are selected.
    /// </summary>
    public bool GenerateAllPublicHeaders { get; init; }

    /// <summary>
    /// Gets additional Clang command-line arguments.
    /// </summary>
    public IReadOnlyList<string> CommandLineArgs { get; init; } = [];

    /// <summary>
    /// Gets the predicate selecting fields for managed projection.
    /// </summary>
    public Func<FieldDecl, bool> FieldTypeToGenerate { get; init; } = _ => true;

    /// <summary>
    /// Gets the explicit vcpkg triplet, or an empty value to use the installed default.
    /// </summary>
    public string Triplet { get; init; } = "";

    /// <summary>
    /// Gets the configured triplet, falling back to the installed default.
    /// </summary>
    /// <param name="defaultsResolver">The installed-triplet resolver.</param>
    /// <returns>The active triplet.</returns>
    internal string GetTriplet(IVcpkgDefaultTripletResolver defaultsResolver)
    {
        ArgumentNullException.ThrowIfNull(defaultsResolver);
        return string.IsNullOrWhiteSpace(Triplet) ? defaultsResolver.GetTriplet() : Triplet;
    }
}