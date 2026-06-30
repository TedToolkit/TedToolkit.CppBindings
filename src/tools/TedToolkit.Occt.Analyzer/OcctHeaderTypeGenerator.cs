// -----------------------------------------------------------------------
// <copyright file="OcctHeaderTypeGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;

using TedToolkit.RoslynHelper.Generators;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.Occt.Analyzer.OcctHeaderTypeGenerator>;

#pragma warning disable RCS0058

namespace TedToolkit.Occt.Analyzer;

/// <summary>
/// Generates the OCCT header type enum from the installed vcpkg headers.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class OcctHeaderTypeGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(Generate);
    }

    private static void Generate(IncrementalGeneratorPostInitializationContext context)
    {
#pragma warning disable RS1035
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
#pragma warning restore RS1035

        if (string.IsNullOrEmpty(vcpkgRoot))
        {
            return;
        }

        var triplet = GetTriplet();
        if (string.IsNullOrEmpty(triplet))
        {
            return;
        }

        var occtSpecificPath = new DirectoryInfo(Path.Combine(vcpkgRoot, "installed", triplet, "include", "opencascade"));
        if (!occtSpecificPath.Exists)
        {
            return;
        }

        context.AddSource("OcctHeaderType.g.cs", GenerateHeaderType(occtSpecificPath).ToCode());
    }

    private static SourceFile GenerateHeaderType(DirectoryInfo directory)
    {
        var enumDeclaration = Enum("OcctHeaderType");

        foreach (var enumerateFile in directory.EnumerateFiles("*.hxx"))
        {
            var name = Path.GetFileNameWithoutExtension(enumerateFile.Name);
            if (name.Contains('.'))
            {
                continue;
            }

            enumDeclaration.AddEnumMember(EnumMember(name));
        }

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt.Generator")
                .AddMember(enumDeclaration));
    }

    private static string GetTriplet()
    {
#pragma warning disable RS1035
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
#pragma warning restore RS1035
        if (string.IsNullOrEmpty(vcpkgRoot))
        {
            return "";
        }

        var installedPath = new DirectoryInfo(Path.Combine(vcpkgRoot, "installed"));
        if (!installedPath.Exists)
        {
            return "";
        }

        var installedTriplets = installedPath.EnumerateDirectories()
            .Select(static directory => directory.Name)
            .Where(folderName => !string.IsNullOrWhiteSpace(folderName)
                                 && new DirectoryInfo(Path.Combine(installedPath.FullName, folderName!, "include", "opencascade")).Exists)
            .Select(static folderName => folderName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static folderName => folderName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (installedTriplets.Length == 0)
        {
            return "";
        }

        var architecturePrefix = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64",
        };

        string[] preferredTriplets;
        string platformToken;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            preferredTriplets = new string[]
            {
                $"{architecturePrefix}-windows",
                $"{architecturePrefix}-windows-static",
                $"{architecturePrefix}-windows-static-md",
            };
            platformToken = "-windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            preferredTriplets = new string[]
            {
                $"{architecturePrefix}-linux",
                $"{architecturePrefix}-linux-release",
            };
            platformToken = "-linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            preferredTriplets = new string[]
            {
                $"{architecturePrefix}-osx",
                $"{architecturePrefix}-osx-static",
            };
            platformToken = "-osx";
        }
        else
        {
            return "";
        }

        foreach (var preferredTriplet in preferredTriplets)
        {
            var exactMatch = installedTriplets.FirstOrDefault(
                installedTriplet => string.Equals(installedTriplet, preferredTriplet, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        var compatibleTriplet = installedTriplets
            .Where(triplet => triplet.Contains(platformToken, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(triplet => triplet.StartsWith(architecturePrefix + "-", StringComparison.OrdinalIgnoreCase))
            .ThenBy(triplet => triplet.Contains("-static", StringComparison.OrdinalIgnoreCase))
            .ThenBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return compatibleTriplet ?? installedTriplets[0];
    }
}
