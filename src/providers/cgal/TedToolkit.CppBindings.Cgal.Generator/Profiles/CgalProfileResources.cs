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
        "TedToolkit.CppBindings.Cgal.Generator.Resources.epick-windows-v1.json";

    private const string HeadersResourcePrefix =
        "TedToolkit.CppBindings.Cgal.Generator.Resources.cgal-6.2-x64-windows.headers.";

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
        return JsonSerializer.Deserialize<CgalProfileManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The embedded CGAL profile is empty.");
    }

    /// <summary>
    /// Loads the complete ordered public-header snapshot.
    /// </summary>
    /// <returns>The ordered paths relative to the vcpkg include root.</returns>
    /// <exception cref="InvalidOperationException">The embedded resource set is missing.</exception>
    internal static IReadOnlyList<string> LoadLockedHeaders()
    {
        var headers = new List<string>();
        var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(static name => name.StartsWith(HeadersResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(".txt", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (resources.Length == 0)
        {
            throw new InvalidOperationException("The embedded CGAL public-header inventory is missing.");
        }

        foreach (var resource in resources)
        {
            using var stream = Open(resource);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    headers.Add(line);
                }
            }
        }

        return Array.AsReadOnly(headers.ToArray());
    }

    private static Stream Open(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded CGAL resource '{name}' is missing.");
    }
}