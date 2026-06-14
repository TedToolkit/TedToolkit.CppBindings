using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.Occt.Analyzer.OcctHeaderTypeGenerator>;

namespace TedToolkit.Occt.Analyzer;

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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "x64-windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "x64-linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "arm64-osx"
                : "x64-osx";
        }

        return "";
    }
}