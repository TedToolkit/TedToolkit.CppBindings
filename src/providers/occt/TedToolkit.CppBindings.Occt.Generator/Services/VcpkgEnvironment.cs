// -----------------------------------------------------------------------
// <copyright file="VcpkgEnvironment.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Cysharp.Text;

using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Provides access to the local vcpkg installation and OCCT metadata.
/// </summary>
internal sealed class VcpkgEnvironment : IVcpkgEnvironment
{
    private const string VCPKG_ROOT_ENVIRONMENT_VARIABLE_NAME = "VCPKG_ROOT";

    private const string INSTALLED_FOLDER_NAME = "installed";

    private const string OCCT_FOLDER_NAME = "opencascade";

    private static readonly Regex StatusBlockSeparator = new("\\r?\\n\\r?\\n", RegexOptions.CultureInvariant);

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
    public string GetIncludeFolder(string triplet)
    {
        return Path.Combine(GetRoot(), INSTALLED_FOLDER_NAME, triplet, "include");
    }

    /// <inheritdoc/>
    public string GetOcctIncludeFolder(string triplet)
    {
        return Path.Combine(GetIncludeFolder(triplet), OCCT_FOLDER_NAME);
    }

    /// <summary>
    /// Gets the installed native package version for one triplet.
    /// </summary>
    /// <param name="triplet">The installed vcpkg triplet.</param>
    /// <param name="packageName">The vcpkg package name.</param>
    /// <returns>The installed native package version.</returns>
    /// <exception cref="InvalidOperationException">The package status is absent, malformed, or ambiguous.</exception>
    internal Version GetInstalledPackageVersion(string triplet, string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        var statusPath = Path.Combine(GetRoot(), INSTALLED_FOLDER_NAME, "vcpkg", "status");
        if (!File.Exists(statusPath))
        {
            throw new InvalidOperationException($"The vcpkg installed-package status file '{statusPath}' was not found.");
        }

        var blocks = StatusBlockSeparator.Split(File.ReadAllText(statusPath))
            .Where(block => HasStatusValue(block, "Package", packageName)
                && HasStatusValue(block, "Architecture", triplet)
                && HasStatusValue(block, "Status", "install ok installed")
                && ReadStatusValue(block, "Version") is not null)
            .ToArray();
        if (blocks.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one installed {packageName} package for triplet '{triplet}', found {blocks.Length}.");
        }

        var versionText = ReadStatusValue(blocks[0], "Version");
        if (Version.TryParse(versionText, out var version))
        {
            return version;
        }

        throw new InvalidOperationException(
            $"Installed {packageName} version '{versionText}' is not a supported native library version.");
    }

    private static bool HasStatusValue(string block, string name, string value)
    {
        return string.Equals(ReadStatusValue(block, name), value, StringComparison.Ordinal);
    }

    private static string? ReadStatusValue(string block, string name)
    {
        const StringSplitOptions Options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
        var prefix = name + ":";
        return block.Split(['\r', '\n',], Options)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..].Trim();
    }

    /// <inheritdoc/>
    public Task<string> GetIncludingHeaderContentAsync(
        string triplet,
        IReadOnlyList<OcctDeclarationOptions> declarations,
        CancellationToken cancellationToken)
    {
        if (declarations is null || declarations.Count is 0)
        {
            throw new InvalidOperationException("At least one target declaration is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var occtIncludeFolder = GetOcctIncludeFolder(triplet);
        var targetStems = new List<string>(declarations.Count);
        var seenTargetStems = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < declarations.Count; index++)
        {
            var targetStem = declarations[index]?.FileName;
            if (!IsValidHeaderStem(targetStem))
            {
                var displayValue = targetStem is null ? "<null>" : $"\"{targetStem}\"";
                throw new InvalidOperationException(
                    $"Target at index {index} has invalid header stem {displayValue}. "
                    + "Expected [A-Za-z_][A-Za-z0-9_]*.");
            }

            var headerPath = Path.Combine(occtIncludeFolder, $"{targetStem}.hxx");
            if (!File.Exists(headerPath))
            {
                throw new InvalidOperationException(
                    $"Target '{targetStem}' does not have a public header below OCCT include root "
                    + $"'{occtIncludeFolder}'. Expected '{headerPath}'.");
            }

            if (seenTargetStems.Add(targetStem))
            {
                targetStems.Add(targetStem);
            }
        }

        var stringBuilder = ZString.CreateStringBuilder();
        foreach (var targetStem in targetStems)
        {
            stringBuilder.Append("#include <");
            stringBuilder.Append(targetStem);
            stringBuilder.AppendLine(".hxx>");
        }

        return Task.FromResult(stringBuilder.ToString());
    }

    private static bool IsValidHeaderStem([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsAsciiIdentifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsAsciiIdentifierPart(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiIdentifierStart(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';
    }

    private static bool IsAsciiIdentifierPart(char value)
    {
        return IsAsciiIdentifierStart(value) || value is >= '0' and <= '9';
    }
}