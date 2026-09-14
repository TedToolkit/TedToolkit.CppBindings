// -----------------------------------------------------------------------
// <copyright file="VcpkgEnvironment.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

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