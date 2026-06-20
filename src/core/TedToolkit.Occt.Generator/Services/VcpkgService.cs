// -----------------------------------------------------------------------
// <copyright file="VcpkgService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using ClangSharp.Interop;

using Cysharp.Text;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Resolves the local vcpkg installation and OCCT metadata.
/// </summary>
public sealed partial class VcpkgService : IVcpkgService
{
    private const string VCPKG_ROOT_ENVIRONMENT_VARIABLE_NAME = "VCPKG_ROOT";

    private const string INSTALLED_FOLDER_NAME = "installed";

    private const string BUILDTREES_FOLDER_NAME = "buildtrees";

    private const string OCCT_FOLDER_NAME = "opencascade";

    private readonly Regex[] _standardPatterns =
    [
        CxxStandardRegex(),
        BuildCppStandardRegex(),
        CmakeStandardRegex(),
    ];

    /// <inheritdoc/>
    public string GetRoot()
    {
#pragma warning disable RS1035
        var vcpkgRoot = Environment.GetEnvironmentVariable(VCPKG_ROOT_ENVIRONMENT_VARIABLE_NAME);
#pragma warning restore RS1035

        if (string.IsNullOrEmpty(vcpkgRoot))
        {
            throw new InvalidOperationException(
                $"{VCPKG_ROOT_ENVIRONMENT_VARIABLE_NAME} cannot be empty. Please check your environment variables.");
        }

        return vcpkgRoot;
    }

    /// <inheritdoc/>
    public string GetIncludeFolder()
    {
        return Path.Combine(GetRoot(), INSTALLED_FOLDER_NAME, GetTriplet(), "include");
    }

    /// <inheritdoc/>
    public string GetTriplet()
    {
        var installedTriplets = GetInstalledOcctTriplets(GetRoot());
        return SelectBestTriplet(installedTriplets, GetCurrentOsPlatform(), RuntimeInformation.ProcessArchitecture);
    }

    /// <inheritdoc/>
    public string GetOcctIncludeFolder()
    {
        return Path.Combine(GetIncludeFolder(), OCCT_FOLDER_NAME);
    }

    /// <inheritdoc/>
    public async Task<int> GetOcctCppVersionAsync()
    {
        var vcpkgRoot = GetRoot();

        var version = await TryGetOcctCppVersionFromBuildTreesAsync(vcpkgRoot).ConfigureAwait(false)
                      ?? await TryGetOcctCppVersionFromInstalledExportsAsync(vcpkgRoot, GetTriplet()).ConfigureAwait(false);

        if (version is not null)
        {
            return version.Value;
        }

        throw new InvalidOperationException(
            $"Could not find OCCT build metadata under {vcpkgRoot}. Expected {BUILDTREES_FOLDER_NAME}/{OCCT_FOLDER_NAME} or {INSTALLED_FOLDER_NAME}/*/share/{OCCT_FOLDER_NAME}.");
    }

    private async Task<int?> TryGetOcctCppVersionFromBuildTreesAsync(string vcpkgRoot)
    {
        var buildtrees = Path.Combine(vcpkgRoot, BUILDTREES_FOLDER_NAME, OCCT_FOLDER_NAME);
        if (!Directory.Exists(buildtrees))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(buildtrees, "*.log", SearchOption.TopDirectoryOnly)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var version = await TryGetOcctCppVersionFromFileAsync(path).ConfigureAwait(false);
            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }

    private async Task<int?> TryGetOcctCppVersionFromInstalledExportsAsync(string vcpkgRoot, string triplet)
    {
        var share = Path.Combine(vcpkgRoot, INSTALLED_FOLDER_NAME, triplet, "share", OCCT_FOLDER_NAME);
        if (!Directory.Exists(share))
        {
            return null;
        }

        foreach (var fileName in new[]
        {
            "OpenCASCADEConfig.cmake",
            "OpenCASCADECompileDefinitionsAndFlags-release.cmake",
            "OpenCASCADECompileDefinitionsAndFlags-debug.cmake",
        })
        {
            var version = await TryGetOcctCppVersionFromFileAsync(Path.Combine(share, fileName)).ConfigureAwait(false);
            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }

    private async Task<int?> TryGetOcctCppVersionFromFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return TryParseOcctCppVersion(text);
    }

    private int? TryParseOcctCppVersion(string text)
    {
        foreach (var pattern in _standardPatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var token = match.Groups["standard"].Value;
            return token switch
            {
                "2a" => 20,
                "2b" => 23,
                _ when int.TryParse(token, out var version) => version,
                _ => null,
            };
        }

        return null;
    }

    [GeneratedRegex(@"(?:/std:c\+\+|-std=(?:gnu\+\+|c\+\+))(?<standard>\d{2}|2[ab])", RegexOptions.CultureInvariant)]
    private static partial Regex CxxStandardRegex();

    [GeneratedRegex(@"BUILD_CPP_STANDARD:STRING=C\+\+(?<standard>\d{2}|2[ab])", RegexOptions.CultureInvariant)]
    private static partial Regex BuildCppStandardRegex();

    [GeneratedRegex(
        @"(?:BUILD_CPP_STANDARD|(?:[A-Z_]*_)?CXX_STANDARD)[^\r\n0-9]*(?:C\+\+)?(?<standard>\d{2}|2[ab])",
        RegexOptions.CultureInvariant)]
    private static partial Regex CmakeStandardRegex();

    internal static string SelectBestTriplet(
        IEnumerable<string> installedTriplets,
        OSPlatform osPlatform,
        Architecture architecture)
    {
        var candidates = installedTriplets
            .Where(static triplet => !string.IsNullOrWhiteSpace(triplet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException("Could not find an installed opencascade triplet.");
        }

        foreach (var preferredTriplet in GetPreferredTriplets(osPlatform, architecture))
        {
            var exactMatch = candidates.FirstOrDefault(
                installedTriplet => string.Equals(installedTriplet, preferredTriplet, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        var compatibleTriplet = candidates
            .Where(triplet => IsCompatibleWithPlatform(triplet, osPlatform))
            .OrderByDescending(triplet => HasArchitecturePrefix(triplet, architecture))
            .ThenBy(IsNotStaticTriplet)
            .ThenBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return compatibleTriplet ?? candidates[0];
    }

    private static IEnumerable<string> GetInstalledOcctTriplets(string vcpkgRoot)
    {
        var installedFolder = Path.Combine(vcpkgRoot, INSTALLED_FOLDER_NAME);
        if (!Directory.Exists(installedFolder))
        {
            return [];
        }

        return Directory.EnumerateDirectories(installedFolder)
            .Select(Path.GetFileName)
#pragma warning disable RCS1112
            .Where(static folderName => !string.IsNullOrWhiteSpace(folderName))
            .Where(folderName => Directory.Exists(Path.Combine(installedFolder, folderName!, "include", OCCT_FOLDER_NAME)))
#pragma warning restore RCS1112
            .Select(static folderName => folderName!);
    }

    private static IEnumerable<string> GetPreferredTriplets(OSPlatform osPlatform, Architecture architecture)
    {
        var architecturePrefix = GetArchitecturePrefix(architecture);

        if (osPlatform == OSPlatform.Windows)
        {
            return
            [
                $"{architecturePrefix}-windows",
                $"{architecturePrefix}-windows-static",
                $"{architecturePrefix}-windows-static-md",
            ];
        }

        if (osPlatform == OSPlatform.Linux)
        {
            return
            [
                $"{architecturePrefix}-linux",
                $"{architecturePrefix}-linux-release",
            ];
        }

        if (osPlatform == OSPlatform.OSX)
        {
            return
            [
                $"{architecturePrefix}-osx",
                $"{architecturePrefix}-osx-static",
            ];
        }

        throw new InvalidOperationException("Can't identify which system it is.");
    }

    private static OSPlatform GetCurrentOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        throw new InvalidOperationException("Can't identify which system it is.");
    }

    private static string GetArchitecturePrefix(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant(),
        };
    }

    private static bool HasArchitecturePrefix(string triplet, Architecture architecture)
    {
        return triplet.StartsWith(GetArchitecturePrefix(architecture) + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompatibleWithPlatform(string triplet, OSPlatform osPlatform)
    {
        string platformToken;
        if (osPlatform == OSPlatform.Windows)
        {
            platformToken = "-windows";
        }
        else if (osPlatform == OSPlatform.Linux)
        {
            platformToken = "-linux";
        }
        else if (osPlatform == OSPlatform.OSX)
        {
            platformToken = "-osx";
        }
        else
        {
            throw new InvalidOperationException("Can't identify which system it is.");
        }

        return triplet.Contains(platformToken, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNotStaticTriplet(string triplet)
    {
        return !triplet.Contains("-static", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> IncludingHeaderContent(CancellationToken cancellationToken)
    {
        var stringBuilder = ZString.CreateStringBuilder();

        stringBuilder.AppendLine("#pragma once");

        foreach (var file in new DirectoryInfo(GetOcctIncludeFolder())
                     .EnumerateFiles("*.hxx"))
        {
            if (await IsDeprecated(file, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            stringBuilder.Append("#include <");
            stringBuilder.Append(file.Name);
            stringBuilder.AppendLine(">");
        }

        return stringBuilder.ToString();
    }

    private static async Task<bool> IsDeprecated(FileInfo file, CancellationToken cancellationToken)
    {
        using var reader = file.OpenText();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Contains(" @deprecated ", StringComparison.InvariantCulture))
            {
                return true;
            }

            if (line.Contains("Standard_HEADER_DEPRECATED", StringComparison.InvariantCulture))
            {
                return true;
            }
        }

        return false;
    }
}