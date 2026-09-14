// -----------------------------------------------------------------------
// <copyright file="CgalProfileResources.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Text.Json;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Loads the immutable resources that define the locked default profile.
/// </summary>
internal static class CgalProfileResources
{
    private const string ManifestResource =
        "TedToolkit.CppBindings.Cgal.Generator.Resources.epick-windows-v2.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Gets the deterministic serializer settings used for emitted inventories.
    /// </summary>
    internal static JsonSerializerOptions JsonOptions { get; } = SerializerOptions;

    /// <summary>
    /// Loads the embedded profile document.
    /// </summary>
    /// <returns>The deserialized profile.</returns>
    /// <exception cref="InvalidOperationException">The embedded resource is missing or empty.</exception>
    internal static CgalProfileManifest LoadManifest()
    {
        using var stream = Open(ManifestResource);
        return ReadManifest(stream, "embedded CGAL profile");
    }

    /// <summary>
    /// Loads an explicit profile document.
    /// </summary>
    /// <param name="file">The profile file.</param>
    /// <returns>The detached immutable profile.</returns>
    /// <exception cref="FileNotFoundException">The explicit profile does not exist.</exception>
    internal static CgalProfileManifest LoadManifest(FileInfo file)
    {
        if (!file.Exists)
        {
            throw new FileNotFoundException("The explicit CGAL profile was not found.", file.FullName);
        }

        using var stream = file.OpenRead();
        return ReadManifest(stream, file.FullName);
    }

    private static Stream Open(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded CGAL resource '{name}' is missing.");
    }

    private static CgalProfileManifest ReadManifest(Stream stream, string source)
    {
        var manifest = JsonSerializer.Deserialize<CgalProfileManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"The CGAL profile '{source}' is empty.");
        return manifest with
        {
            SelectedHeaders = Array.AsReadOnly(manifest.SelectedHeaders.ToArray()),
            Declarations = Array.AsReadOnly(manifest.Declarations.Select(static declaration => declaration with
            {
                Dependencies = Array.AsReadOnly(declaration.Dependencies.ToArray()),
            }).ToArray()),
            Toolchain = manifest.Toolchain with { },
        };
    }
}