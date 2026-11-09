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

        var descriptor = ProviderDescriptor.Create(Require(values, "--provider").ToLowerInvariant());
        var hasVcpkgRoot = values.ContainsKey("--vcpkg-root");
        var hasInstallPrefix = values.ContainsKey("--install-prefix");
        if (hasVcpkgRoot == hasInstallPrefix)
        {
            throw Usage();
        }

        if (hasInstallPrefix && descriptor.Id is not "manifold")
        {
            throw new ArgumentException($"An installation prefix is not supported for {descriptor.DisplayName}.");
        }

        INativeSourceProvider source = hasVcpkgRoot
            ? new VcpkgNativeSourceProvider(new(Require(values, "--vcpkg-root")), descriptor)
            : new ManifoldInstallSourceProvider(new(Require(values, "--install-prefix")));
        return new(
            descriptor,
            new(Require(values, "--repository-root")),
            new(Require(values, "--output-root")),
            source,
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
            + "--output-root <path> (--vcpkg-root <path> | --install-prefix <path>) --configuration <name>.");
    }
}

internal sealed record WindowsGenerationOptions(
    ProviderDescriptor Descriptor,
    DirectoryPath RepositoryRoot,
    DirectoryPath OutputRoot,
    INativeSourceProvider Source,
    string Configuration);
