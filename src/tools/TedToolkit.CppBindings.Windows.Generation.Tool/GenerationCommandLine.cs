// -----------------------------------------------------------------------
// <copyright file="GenerationCommandLine.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

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

        return new(
            Require(values, "--provider"),
            new(Require(values, "--repository-root")),
            new(Require(values, "--output-root")),
            new(Require(values, "--vcpkg-root")),
            Require(values, "--configuration"));
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
            "Expected --provider <occt|cgal|manifold|fcl> --repository-root <path> "
            + "--output-root <path> --vcpkg-root <path> --configuration <name>.");
    }
}

internal sealed record WindowsGenerationOptions(
    string Provider,
    DirectoryInfo RepositoryRoot,
    DirectoryInfo OutputRoot,
    DirectoryInfo VcpkgRoot,
    string Configuration);