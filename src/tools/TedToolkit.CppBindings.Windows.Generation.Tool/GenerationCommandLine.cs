// -----------------------------------------------------------------------
// <copyright file="GenerationCommandLine.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal static class GenerationCommandLine
{
    internal static WindowsGenerationOptions Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length % 2 is not 0)
        {
            throw Usage();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                throw new ArgumentException($"Duplicate argument '{arguments[index]}'.", nameof(arguments));
            }
        }

        var provider = ProviderDescriptor.Create(Require(values, "--provider").ToLowerInvariant());
        var vcpkgRoot = provider.UsesVcpkg
            ? new DirectoryPath(Require(values, "--vcpkg-root"))
            : (DirectoryPath?)null;
        var runtimeIdentifier = values.GetValueOrDefault("--runtime-identifier", "win-x64");
        if (runtimeIdentifier is not ("win-x64" or "linux-x64" or "osx-arm64"))
        {
            throw Usage();
        }

        return new(
            provider,
            new(Require(values, "--repository-root")),
            new(Require(values, "--output-root")),
            vcpkgRoot,
            Require(values, "--configuration"))
        {
            RuntimeIdentifier = runtimeIdentifier,
        };
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string name)
    {
        if (values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw Usage();
    }

    private static ArgumentException Usage()
    {
        return new(
            "Expected --provider <occt|cgal|manifold|fcl|smlib> --repository-root <path> "
            + "--output-root <path> --configuration <name> and --vcpkg-root <path> for vcpkg providers. "
            + "On Linux and macOS pass --runtime-identifier <linux-x64|osx-arm64>.");
    }
}

internal sealed record WindowsGenerationOptions(
    ProviderDescriptor Provider,
    DirectoryPath RepositoryRoot,
    DirectoryPath OutputRoot,
    DirectoryPath? VcpkgRoot,
    string Configuration)
{
    internal string RuntimeIdentifier { get; init; } = "win-x64";
}