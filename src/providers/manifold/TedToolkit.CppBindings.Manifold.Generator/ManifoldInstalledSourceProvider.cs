// -----------------------------------------------------------------------
// <copyright file="ManifoldInstalledSourceProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.CppBindings.Generator;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Manifold.Generator;

internal sealed class ManifoldInstalledSourceProvider(DirectoryPath prefix, ManifoldProfile profile)
    : INativeSourceProvider
{
    public NativeSourceInputs Resolve(CancellationToken cancellationToken)
    {
        var includeRoot = prefix / "include";
        var inputs = ManifoldHeaderDiscovery.ReadInputs(includeRoot, cancellationToken);
        ValidateInstalledVersion(includeRoot / "manifold", profile);
        return inputs;
    }

    private static void ValidateInstalledVersion(in DirectoryPath manifoldRoot, ManifoldProfile profile)
    {
        var versionHeader = manifoldRoot / new FileName("version.h");
        var version = Version.Parse(profile.ManifoldVersion);
        var content = versionHeader.Exists ? versionHeader.ReadAllText() : "";
        if (HasVersionDefine(content, "MAJOR", version.Major)
            && HasVersionDefine(content, "MINOR", version.Minor)
            && HasVersionDefine(content, "PATCH", version.Build))
        {
            return;
        }

        throw new InvalidOperationException(
            $"The Manifold version header '{versionHeader}' is missing or does not match profile version {version}.");
    }

    private static bool HasVersionDefine(string content, string component, int value)
    {
        return Regex.IsMatch(content, $@"(?m)^#define[ \t]+MANIFOLD_VERSION_{component}[ \t]+{value}[ \t]*\r?$",
            RegexOptions.CultureInvariant);
    }
}