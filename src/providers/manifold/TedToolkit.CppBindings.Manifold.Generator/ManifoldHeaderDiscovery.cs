// -----------------------------------------------------------------------
// <copyright file="ManifoldHeaderDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.CppBindings.Generator;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Reads and classifies the public headers of a Manifold include tree.</summary>
internal static partial class ManifoldHeaderDiscovery
{
    private static readonly string[] ProfileRoots = ["manifold/manifold.h", "manifold/mesh.h",];

    internal static NativeSourceInputs ReadInputs(DirectoryPath includeRoot, in CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifoldRoot = includeRoot / "manifold";
        if (!manifoldRoot.Exists)
        {
            throw new DirectoryNotFoundException(
                $"Manifold public headers were not found beneath '{manifoldRoot}'.");
        }

        var installed = manifoldRoot.EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(path => Normalize(path.GetRelativePath(includeRoot).FullName))
            .Order(StringComparer.Ordinal);
        return new NativeSourceInputs(includeRoot, installed);
    }

    internal static IReadOnlyList<ManifoldSourceDisposition> Classify(NativeSourceInputs inputs)
    {
        var includeRoot = inputs.IncludeRoot;
        var manifoldRoot = includeRoot / "manifold";
        var roots = ProfileRoots.ToHashSet(StringComparer.Ordinal);
        var reachable = CloseIncludes(includeRoot.FullName, manifoldRoot.FullName, ProfileRoots);
        return Array.AsReadOnly(inputs.PublicHeaders.Select(header => new ManifoldSourceDisposition(
                header,
                GetDisposition(header, roots, reachable)))
            .ToArray());
    }

    private static string GetDisposition(
        string header,
        HashSet<string> roots,
        HashSet<string> reachable)
    {
        if (roots.Contains(header))
        {
            return "profile-root";
        }

        return reachable.Contains(header)
            ? "reachable-dependency"
            : "not-reachable-from-finite-profile";
    }

    private static HashSet<string> CloseIncludes(
        string includeRoot,
        string manifoldRoot,
        IEnumerable<string> roots)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(roots.Order(StringComparer.Ordinal));
        var allowedPrefix = Path.GetFullPath(manifoldRoot) + Path.DirectorySeparatorChar;
        while (pending.TryDequeue(out var header))
        {
            if (!closure.Add(header))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(includeRoot, header.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Profile header '{header}' is absent from the installed package.");
            }

            foreach (Match match in HeaderInclude().Matches(File.ReadAllText(path)))
            {
                var dependency = match.Groups["path"].Value;
                var candidate = dependency.StartsWith("manifold/", StringComparison.Ordinal)
                    ? Path.Combine(includeRoot, dependency.Replace('/', Path.DirectorySeparatorChar))
                    : Path.Combine(Path.GetDirectoryName(path)!, dependency.Replace('/', Path.DirectorySeparatorChar));
                var normalizedPath = Path.GetFullPath(candidate);
                if (normalizedPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(normalizedPath))
                {
                    pending.Enqueue(Normalize(Path.GetRelativePath(includeRoot, normalizedPath)));
                }
            }
        }

        return closure;
    }

    internal static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    [GeneratedRegex("(?m)^\\s*#\\s*include\\s*[<\\\"](?<path>[^>\\\"]+)[>\\\"]", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderInclude();
}