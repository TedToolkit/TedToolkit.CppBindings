// -----------------------------------------------------------------------
// <copyright file="FclHeaderDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Resolves the installed FCL public-header boundary.</summary>
internal static partial class FclHeaderDiscovery
{
    private const string ProfileRoot = "fcl/fcl.h";

    /// <summary>Resolves and validates the deterministic installed source inventory.</summary>
    /// <exception cref="DirectoryNotFoundException">The installed FCL header tree is absent.</exception>
    /// <exception cref="InvalidOperationException">The package identity or header inventory is inconsistent.</exception>
    internal static IReadOnlyList<FclSourceDisposition> Resolve(DirectoryInfo vcpkgRoot, FclProfile profile)
    {
        var triplet = profile.Versions["Triplet"];
        var includeRoot = Path.Combine(vcpkgRoot.FullName, "installed", triplet, "include");
        var fclRoot = Path.Combine(includeRoot, "fcl");
        if (!Directory.Exists(fclRoot))
        {
            throw new DirectoryNotFoundException($"FCL public headers were not found beneath '{fclRoot}'.");
        }

        var installed = Directory.EnumerateFiles(fclRoot, "*", SearchOption.AllDirectories)
            .Select(path => Normalize(Path.GetRelativePath(includeRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var identity = ParseIdentity(profile.Versions["Fcl"]);
        ValidatePackageStatus(vcpkgRoot, triplet, identity);
        var packaged = ResolvePackageHeaders(vcpkgRoot, triplet, identity.Version);
        if (!installed.SequenceEqual(packaged, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The installed FCL public-header inventory does not match the vcpkg package list.");
        }

        var reachable = CloseIncludes(includeRoot, fclRoot);
        return Array.AsReadOnly(installed.Select(header => new FclSourceDisposition(
                header,
                GetDisposition(header, reachable)))
            .ToArray());
    }

    private static string GetDisposition(string header, HashSet<string> reachable)
    {
        if (string.Equals(header, ProfileRoot, StringComparison.Ordinal))
        {
            return "profile-root";
        }

        return reachable.Contains(header)
            ? "reachable-dependency"
            : "not-reachable-from-finite-profile";
    }

    private static FclPackageIdentity ParseIdentity(string value)
    {
        var parts = value.Split('#', 2);
        return new FclPackageIdentity(parts[0], parts.Length == 2 ? parts[1] : null);
    }

    private static void ValidatePackageStatus(
        DirectoryInfo vcpkgRoot,
        string triplet,
        FclPackageIdentity identity)
    {
        var statusPath = Path.Combine(vcpkgRoot.FullName, "installed", "vcpkg", "status");
        if (!File.Exists(statusPath))
        {
            throw new InvalidOperationException($"The vcpkg installed-package status file '{statusPath}' was not found.");
        }

        var blocks = PackageStatusBlock().Matches(File.ReadAllText(statusPath))
            .Select(static match => match.Groups["body"].Value)
            .Where(body => HasStatusValue(body, "Architecture", triplet)
                && HasStatusValue(body, "Version", identity.Version)
                && HasPortVersion(body, identity.PortVersion)
                && HasStatusValue(body, "Status", "install ok installed"))
            .ToArray();
        if (blocks.Length == 1)
        {
            return;
        }

        var expected = identity.PortVersion is null
            ? identity.Version
            : identity.Version + "#" + identity.PortVersion;
        throw new InvalidOperationException($"Expected installed FCL {expected} for triplet '{triplet}'.");
    }

    private static bool HasPortVersion(string block, string? portVersion)
    {
        return portVersion is null
            ? !PortVersionStatusLine().IsMatch(block)
            : HasStatusValue(block, "Port-Version", portVersion);
    }

    private static bool HasStatusValue(string block, string name, string value)
    {
        return Regex.IsMatch(
            block,
            $"(?m)^{Regex.Escape(name)}: {Regex.Escape(value)}\\r?$",
            RegexOptions.CultureInvariant);
    }

    private static string[] ResolvePackageHeaders(DirectoryInfo vcpkgRoot, string triplet, string version)
    {
        var infoRoot = Path.Combine(vcpkgRoot.FullName, "installed", "vcpkg", "info");
        if (!Directory.Exists(infoRoot))
        {
            throw new InvalidOperationException(
                $"The vcpkg installed-package list directory '{infoRoot}' was not found.");
        }

        var suffix = $"_{triplet}.list";
        var lists = Directory.EnumerateFiles(infoRoot, "fcl_*.list", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        var expectedName = $"fcl_{version}_{triplet}.list";
        if (lists.Length != 1 || !string.Equals(Path.GetFileName(lists[0]), expectedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected the installed FCL package list '{expectedName}', found {lists.Length} candidate(s).");
        }

        var includePrefix = $"{triplet}/include/";
        var headers = File.ReadLines(lists[0])
            .Select(Normalize)
            .Where(line => line.StartsWith(includePrefix + "fcl/", StringComparison.Ordinal)
                && !line.EndsWith('/'))
            .Select(line => line[includePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (headers.Length == 0)
        {
            throw new InvalidOperationException(
                $"The installed FCL package list for triplet '{triplet}' contains no public headers.");
        }

        return headers;
    }

    private static HashSet<string> CloseIncludes(string includeRoot, string fclRoot)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>([ProfileRoot,]);
        var allowedPrefix = Path.GetFullPath(fclRoot) + Path.DirectorySeparatorChar;
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
                var candidate = dependency.StartsWith("fcl/", StringComparison.Ordinal)
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

    [GeneratedRegex("(?ms)^Package: fcl\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\z)", RegexOptions.CultureInvariant)]
    private static partial Regex PackageStatusBlock();

    [GeneratedRegex("(?m)^Port-Version:", RegexOptions.CultureInvariant)]
    private static partial Regex PortVersionStatusLine();
}
