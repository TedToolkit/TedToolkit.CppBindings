// -----------------------------------------------------------------------
// <copyright file="ManifoldHeaderDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Resolves the installed Manifold public-header boundary.</summary>
internal static partial class ManifoldHeaderDiscovery
{
    private static readonly string[] ProfileRoots = ["manifold/manifold.h", "manifold/mesh.h",];

    /// <summary>Resolves and validates the deterministic installed source inventory.</summary>
    /// <exception cref="DirectoryNotFoundException">The installed Manifold header tree is absent.</exception>
    /// <exception cref="InvalidOperationException">The package identity or header inventory is inconsistent.</exception>
    internal static IReadOnlyList<ManifoldSourceDisposition> Resolve(
        DirectoryInfo vcpkgRoot,
        ManifoldProfile profile)
    {
        var includeRoot = Path.Combine(vcpkgRoot.FullName, "installed", profile.Triplet, "include");
        var manifoldRoot = Path.Combine(includeRoot, "manifold");
        if (!Directory.Exists(manifoldRoot))
        {
            throw new DirectoryNotFoundException(
                $"Manifold public headers were not found beneath '{manifoldRoot}'.");
        }

        var installed = Directory.EnumerateFiles(manifoldRoot, "*", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(includeRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        ValidatePackageStatus(vcpkgRoot, profile);
        var packaged = ResolvePackageHeaders(vcpkgRoot, profile);
        if (!installed.SequenceEqual(packaged, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The installed Manifold public-header inventory does not match the vcpkg package list.");
        }

        var roots = ProfileRoots.ToHashSet(StringComparer.Ordinal);
        var reachable = CloseIncludes(includeRoot, manifoldRoot, ProfileRoots);
        return Array.AsReadOnly(installed.Select(header => new ManifoldSourceDisposition(
                header,
                GetDisposition(header, roots, reachable)))
            .ToArray());
    }

    private static void ValidatePackageStatus(DirectoryInfo vcpkgRoot, ManifoldProfile profile)
    {
        var statusPath = Path.Combine(vcpkgRoot.FullName, "installed", "vcpkg", "status");
        if (!File.Exists(statusPath))
        {
            throw new InvalidOperationException($"The vcpkg installed-package status file '{statusPath}' was not found.");
        }

        var blocks = PackageStatusBlock().Matches(File.ReadAllText(statusPath))
            .Select(static match => match.Groups["body"].Value)
            .Where(body => HasStatusValue(body, "Architecture", profile.Triplet)
                && HasStatusValue(body, "Version", profile.ManifoldVersion)
                && !PortVersionStatusLine().IsMatch(body)
                && HasStatusValue(body, "Status", "install ok installed"))
            .ToArray();
        if (blocks.Length == 1)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected installed Manifold {profile.ManifoldVersion} for triplet '{profile.Triplet}'.");
    }

    private static bool HasStatusValue(string block, string name, string value)
    {
        return Regex.IsMatch(
            block,
            $"(?m)^{Regex.Escape(name)}: {Regex.Escape(value)}\\r?$",
            RegexOptions.CultureInvariant);
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

    private static string[] ResolvePackageHeaders(DirectoryInfo vcpkgRoot, ManifoldProfile profile)
    {
        var infoRoot = Path.Combine(vcpkgRoot.FullName, "installed", "vcpkg", "info");
        if (!Directory.Exists(infoRoot))
        {
            throw new InvalidOperationException(
                $"The vcpkg installed-package list directory '{infoRoot}' was not found.");
        }

        var suffix = $"_{profile.Triplet}.list";
        var lists = Directory.EnumerateFiles(infoRoot, "manifold_*.list", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        var expectedName = $"manifold_{profile.ManifoldVersion}_{profile.Triplet}.list";
        if (lists.Length != 1 || !string.Equals(Path.GetFileName(lists[0]), expectedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected the installed Manifold package list '{expectedName}', found {lists.Length} candidate(s).");
        }

        var includePrefix = $"{profile.Triplet}/include/";
        var headers = File.ReadLines(lists[0])
            .Select(Normalize)
            .Where(line => line.StartsWith(includePrefix + "manifold/", StringComparison.Ordinal)
                && !line.EndsWith('/'))
            .Select(line => line[includePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (headers.Length == 0)
        {
            throw new InvalidOperationException(
                $"The installed Manifold package list for triplet '{profile.Triplet}' contains no public headers.");
        }

        return headers;
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

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    [GeneratedRegex("(?m)^\\s*#\\s*include\\s*[<\\\"](?<path>[^>\\\"]+)[>\\\"]", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderInclude();

    [GeneratedRegex("(?ms)^Package: manifold\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\z)", RegexOptions.CultureInvariant)]
    private static partial Regex PackageStatusBlock();

    [GeneratedRegex("(?m)^Port-Version:", RegexOptions.CultureInvariant)]
    private static partial Regex PortVersionStatusLine();
}