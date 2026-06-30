// -----------------------------------------------------------------------
// <copyright file="VcpkgEnvironment.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Provides access to the local vcpkg installation and OCCT metadata.
/// </summary>
public sealed class VcpkgEnvironment : IVcpkgEnvironment
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

    public async Task<string> IncludingHeaderContent(string triplet, CancellationToken cancellationToken)
    {
        var stringBuilder = ZString.CreateStringBuilder();

        stringBuilder.AppendLine("#pragma once");

        foreach (var file in new DirectoryInfo(GetOcctIncludeFolder(triplet))
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
