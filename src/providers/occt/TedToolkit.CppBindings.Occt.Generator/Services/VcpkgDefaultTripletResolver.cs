// -----------------------------------------------------------------------
// <copyright file="VcpkgDefaultTripletResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Resolves default generation values from the current vcpkg installation.
/// </summary>
internal sealed class VcpkgDefaultTripletResolver : IVcpkgDefaultTripletResolver
{
    private const string VCPKG_ROOT_ENVIRONMENT_VARIABLE_NAME = "VCPKG_ROOT";

    private const string INSTALLED_FOLDER_NAME = "installed";

    private const string OCCT_FOLDER_NAME = "opencascade";

    /// <inheritdoc/>
    public string GetTriplet()
    {
        var installedTriplets = GetInstalledOcctTriplets(GetRoot());
        return SelectBestTriplet(installedTriplets, GetCurrentOsPlatform(), RuntimeInformation.ProcessArchitecture);
    }

    private static string GetRoot()
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

    /// <summary>
    /// Selects the best installed triplet for the current platform and architecture.
    /// </summary>
    /// <param name="installedTriplets">The installed triplets.</param>
    /// <param name="osPlatform">The current operating system platform.</param>
    /// <param name="architecture">The current process architecture.</param>
    /// <returns>The selected triplet.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no compatible triplet can be found.</exception>
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

    private static IEnumerable<string> GetPreferredTriplets(in OSPlatform osPlatform, Architecture architecture)
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

    /// <summary>
    /// Gets the preferred lowercase architecture token used by vcpkg triplets.
    /// </summary>
    /// <param name="architecture">The process architecture.</param>
    /// <returns>The triplet architecture token.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the architecture is unsupported.</exception>
    private static string GetArchitecturePrefix(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => throw new ArgumentOutOfRangeException(
                nameof(architecture),
                architecture,
                "Unsupported architecture."),
        };
    }

    private static bool HasArchitecturePrefix(string triplet, Architecture architecture)
    {
        return triplet.StartsWith(GetArchitecturePrefix(architecture) + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompatibleWithPlatform(string triplet, in OSPlatform osPlatform)
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
}