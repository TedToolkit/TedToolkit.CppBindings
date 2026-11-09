// -----------------------------------------------------------------------
// <copyright file="ManifoldVcpkgSourceProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.CppBindings.Generator;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Manifold.Generator;

internal sealed partial class ManifoldVcpkgSourceProvider(DirectoryPath root, ManifoldProfile profile)
    : INativeSourceProvider
{
    public NativeSourceInputs Resolve(CancellationToken cancellationToken)
    {
        var includeRoot = root / "installed" / profile.Triplet / "include";
        var inputs = ManifoldHeaderDiscovery.ReadInputs(includeRoot, cancellationToken);
        ValidateVcpkgInventory(root, profile, inputs.PublicHeaders);
        return inputs;
    }

    private static void ValidateVcpkgInventory(
        in DirectoryPath vcpkgRoot,
        ManifoldProfile profile,
        IReadOnlyList<string> installed)
    {
        ValidatePackageStatus(vcpkgRoot, profile);
        var packaged = ResolvePackageHeaders(vcpkgRoot, profile);
        if (installed.SequenceEqual(packaged, StringComparer.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            "The installed Manifold public-header inventory does not match the vcpkg package list.");
    }

    private static void ValidatePackageStatus(in DirectoryPath vcpkgRoot, ManifoldProfile profile)
    {
        var statusPath = vcpkgRoot / "installed" / "vcpkg" / new FileName("status");
        if (!statusPath.Exists)
        {
            throw new InvalidOperationException($"The vcpkg installed-package status file '{statusPath}' was not found.");
        }

        var blocks = PackageStatusBlock().Matches(statusPath.ReadAllText())
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

    private static string[] ResolvePackageHeaders(in DirectoryPath vcpkgRoot, ManifoldProfile profile)
    {
        var infoRoot = vcpkgRoot / "installed" / "vcpkg" / "info";
        if (!infoRoot.Exists)
        {
            throw new InvalidOperationException(
                $"The vcpkg installed-package list directory '{infoRoot}' was not found.");
        }

        var suffix = $"_{profile.Triplet}.list";
        var lists = infoRoot.EnumerateFiles("manifold_*.list", SearchOption.TopDirectoryOnly)
            .Where(path => path.Name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        var expectedName = $"manifold_{profile.ManifoldVersion}_{profile.Triplet}.list";
        if (lists.Length != 1 || !string.Equals(lists[0].Name, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected the installed Manifold package list '{expectedName}', found {lists.Length} candidate(s).");
        }

        var includePrefix = $"{profile.Triplet}/include/";
        var headers = lists[0].ReadLines()
            .Select(ManifoldHeaderDiscovery.Normalize)
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

    [GeneratedRegex("(?ms)^Package: manifold\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\z)", RegexOptions.CultureInvariant)]
    private static partial Regex PackageStatusBlock();

    [GeneratedRegex("(?m)^Port-Version:", RegexOptions.CultureInvariant)]
    private static partial Regex PortVersionStatusLine();
}