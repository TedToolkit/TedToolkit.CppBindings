// -----------------------------------------------------------------------
// <copyright file="VcpkgHeaderInventory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Reads installed vcpkg header lists and classifies the headers of locked binding profiles.
/// </summary>
public static partial class VcpkgHeaderInventory
{
    /// <summary>
    /// Verifies a locked package and classifies its public headers by reachability from the profile roots.
    /// </summary>
    /// <param name="installationRoot">The vcpkg installation directory containing the triplet and package metadata.</param>
    /// <param name="triplet">The installed triplet.</param>
    /// <param name="packageName">The package name used in diagnostics. The port and include directory use its lowercase form.</param>
    /// <param name="lockedVersion">The version, optionally followed by <c>#</c> and the port revision.</param>
    /// <param name="profileHeaders">The entry headers relative to the triplet include directory.</param>
    /// <returns>The installed header paths and their dispositions, ordered by path.</returns>
    /// <exception cref="DirectoryNotFoundException">The package's include directory is absent.</exception>
    /// <exception cref="InvalidOperationException">The installed package or its headers do not match the profile.</exception>
    public static (string Header, string Disposition)[] ResolveLocked(
        in DirectoryPath installationRoot,
        string triplet,
        string packageName,
        string lockedVersion,
        IReadOnlyCollection<string> profileHeaders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedVersion);
        ArgumentNullException.ThrowIfNull(profileHeaders);
        var includeRoot = NativeSources.GetVcpkgIncludeRoot(installationRoot, triplet);
        var packageRoot = includeRoot / GetPortName(packageName);
        if (!packageRoot.Exists)
        {
            throw new DirectoryNotFoundException($"{packageName} public headers were not found beneath '{packageRoot}'.");
        }

        var installed = packageRoot.EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(path => Normalize(path.GetRelativePath(includeRoot).FullName))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var versionParts = lockedVersion.Split('#', 2);
        var version = versionParts[0];
        var portVersion = versionParts.Length == 2 ? versionParts[1] : null;
        VerifyPackageStatus(installationRoot, triplet, packageName, version, portVersion, lockedVersion);
        var packaged = ReadPackageHeaders(installationRoot, triplet, packageName, packageRoot.Name, version);
        if (!installed.SequenceEqual(packaged, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The installed {packageName} public-header inventory does not match the vcpkg package list.");
        }

        var roots = profileHeaders.ToHashSet(StringComparer.Ordinal);
        var reachable = CloseIncludes(includeRoot.FullName, packageRoot.FullName, packageRoot.Name, roots);
        return installed.Select(header => (header, GetDisposition(header, roots, reachable)))
            .ToArray();
    }

    /// <summary>
    /// Reads the public headers listed for a single installed package.
    /// </summary>
    /// <param name="installationRoot">The vcpkg installation directory containing package metadata.</param>
    /// <param name="triplet">The installed triplet.</param>
    /// <param name="packageName">The package name used in diagnostics. The port uses its lowercase form.</param>
    /// <param name="includeDirectoryName">The case-sensitive package directory under the triplet include root.</param>
    /// <param name="version">The required version in the list filename, when the profile locks it.</param>
    /// <returns>The package's include-relative header paths, ordered by path.</returns>
    /// <exception cref="InvalidOperationException">A single matching package list with headers could not be found.</exception>
    public static string[] ReadPackageHeaders(
        in DirectoryPath installationRoot,
        string triplet,
        string packageName,
        string includeDirectoryName,
        string? version = null)
    {
        var port = GetPortName(packageName);
        var infoRoot = installationRoot / "vcpkg" / "info";
        if (!infoRoot.Exists)
        {
            throw new InvalidOperationException(
                $"The vcpkg installed-package list directory '{infoRoot}' was not found.");
        }

        var suffix = $"_{triplet}.list";
        var lists = infoRoot.EnumerateFiles($"{port}_*.list", SearchOption.TopDirectoryOnly)
            .Where(path => path.Name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();
        var expectedName = version is null ? null : $"{port}_{version}_{triplet}.list";
        if (lists.Length != 1
            || (expectedName is not null && !string.Equals(lists[0].Name, expectedName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(expectedName is null
                ? $"Expected one installed {packageName} package list for triplet '{triplet}', found {lists.Length}."
                : $"Expected the installed {packageName} package list '{expectedName}', found {lists.Length} candidate(s).");
        }

        var includePrefix = $"{triplet}/include/";
        var packagePrefix = $"{includePrefix}{includeDirectoryName}/";
        var headers = lists[0].ReadLines()
            .Select(Normalize)
            .Where(line => line.StartsWith(packagePrefix, StringComparison.Ordinal) && !line.EndsWith('/'))
            .Select(line => line[includePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (headers.Length == 0)
        {
            throw new InvalidOperationException(
                $"The installed {packageName} package list for triplet '{triplet}' contains no public headers.");
        }

        return headers;
    }

    private static void VerifyPackageStatus(
        in DirectoryPath installationRoot,
        string triplet,
        string packageName,
        string version,
        string? portVersion,
        string lockedVersion)
    {
        var statusPath = installationRoot / "vcpkg" / new FileName("status");
        if (!statusPath.Exists)
        {
            throw new InvalidOperationException($"The vcpkg installed-package status file '{statusPath}' was not found.");
        }

        var port = Regex.Escape(GetPortName(packageName));
        var blocks = Regex.Matches(statusPath.ReadAllText(),
                $"(?ms)^Package: {port}\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\z)",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["body"].Value)
            .Count(body => HasStatusValue(body, "Architecture", triplet)
                && HasStatusValue(body, "Version", version)
                && (portVersion is null
                    ? !PortVersionStatusLine().IsMatch(body)
                    : HasStatusValue(body, "Port-Version", portVersion))
                && HasStatusValue(body, "Status", "install ok installed"));
        if (blocks == 1)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected installed {packageName} {lockedVersion} for triplet '{triplet}'.");
    }

    private static string GetPortName(string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        if (!packageName.All(static value => char.IsAsciiLetterOrDigit(value) || value == '-'))
        {
            throw new ArgumentException("Expected a vcpkg package name.", nameof(packageName));
        }

        // vcpkg stores port identifiers in lowercase in its status file and package list names.
#pragma warning disable CA1308
        return packageName.ToLowerInvariant();
#pragma warning restore CA1308
    }

    private static bool HasStatusValue(string block, string name, string value)
    {
        return Regex.IsMatch(
            block,
            $"(?m)^{Regex.Escape(name)}: {Regex.Escape(value)}\\r?$",
            RegexOptions.CultureInvariant);
    }

    private static HashSet<string> CloseIncludes(
        string includeRoot,
        string packageRoot,
        string includeDirectoryName,
        HashSet<string> roots)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(roots.Order(StringComparer.Ordinal));
        var allowedPrefix = Path.GetFullPath(packageRoot) + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var includePrefix = includeDirectoryName + "/";
        while (pending.TryDequeue(out var header))
        {
            if (!closure.Add(header))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(includeRoot, header.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(allowedPrefix, pathComparison) || !File.Exists(path))
            {
                throw new InvalidOperationException($"Profile header '{header}' is absent from the installed package.");
            }

            foreach (Match match in HeaderInclude().Matches(File.ReadAllText(path)))
            {
                var dependency = match.Groups["path"].Value;
                var relativeTo = dependency.StartsWith(includePrefix, StringComparison.Ordinal)
                    ? includeRoot
                    : Path.GetDirectoryName(path)!;
                var normalizedPath = Path.GetFullPath(
                    dependency.Replace('/', Path.DirectorySeparatorChar),
                    relativeTo);
                if (normalizedPath.StartsWith(allowedPrefix, pathComparison)
                    && File.Exists(normalizedPath))
                {
                    pending.Enqueue(Normalize(Path.GetRelativePath(includeRoot, normalizedPath)));
                }
            }
        }

        return closure;
    }

    private static string GetDisposition(string header, HashSet<string> roots, HashSet<string> reachable)
    {
        if (roots.Contains(header))
        {
            return "profile-root";
        }

        return reachable.Contains(header) ? "reachable-dependency" : "not-reachable-from-finite-profile";
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    [GeneratedRegex("""(?m)^\s*#\s*include\s*[<"](?<path>[^>"]+)[>"]""", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderInclude();

    [GeneratedRegex("(?m)^Port-Version:", RegexOptions.CultureInvariant)]
    private static partial Regex PortVersionStatusLine();
}