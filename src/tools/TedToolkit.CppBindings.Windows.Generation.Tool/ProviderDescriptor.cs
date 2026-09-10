// -----------------------------------------------------------------------
// <copyright file="ProviderDescriptor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal enum NativeBuildKind
{
    Occt,
    VisualStudio,
}

internal enum ToolchainProfileKind
{
    None,
    Cgal,
    Manifold,
    Fcl,
}

internal sealed record VcpkgInputPattern(string RelativeRoot, string SearchPattern);

internal sealed record ProviderDescriptor(
    string Id,
    string DisplayName,
    string NativeLibraryName,
    NativeBuildKind NativeBuild,
    ToolchainProfileKind ToolchainProfile,
    bool WriteManagedManifest,
    bool WriteOutputManifest,
    bool IncludeVcpkgPathInFingerprint,
    string? RequiredManagedFile,
    IReadOnlyList<string> RepositoryInputRoots,
    IReadOnlyList<string> RepositoryInputFiles,
    IReadOnlyList<string> VcpkgInputRoots,
    IReadOnlyList<VcpkgInputPattern> VcpkgInputPatterns,
    IReadOnlyDictionary<string, string> Notices)
{
    internal static ProviderDescriptor Create(string provider)
    {
        return provider switch
        {
            "occt" => new(
                "occt",
                "OCCT",
                "ted_toolkit_occt.dll",
                NativeBuildKind.Occt,
                ToolchainProfileKind.None,
                WriteManagedManifest: true,
                WriteOutputManifest: false,
                IncludeVcpkgPathInFingerprint: true,
                RequiredManagedFile: null,
                [
                    "src/shared/TedToolkit.CppBindings.Generator",
                    "src/providers/occt/TedToolkit.CppBindings.Occt.Generator",
                    "src/providers/occt/TedToolkit.CppBindings.Occt.SourceGenerators",
                    "externals/TedToolkit/TedToolkit.RoslynHelper",
                    "externals/TedToolkit/props",
                    "src/tools/TedToolkit.CppBindings.Windows.Generation.Tool",
                ],
                [
                    "Directory.Build.props",
                    "Directory.Build.targets",
                    "Directory.Packages.props",
                    "src/providers/occt/TedToolkit.CppBindings.Occt.Windows/TedToolkit.CppBindings.Occt.Windows.csproj",
                ],
                [],
                [],
                new Dictionary<string, string>()),
            "cgal" => new(
                "cgal",
                "CGAL",
                "ted_toolkit_cpp_bindings_cgal.dll",
                NativeBuildKind.VisualStudio,
                ToolchainProfileKind.Cgal,
                WriteManagedManifest: true,
                WriteOutputManifest: true,
                IncludeVcpkgPathInFingerprint: false,
                RequiredManagedFile: null,
                [
                    "src/shared/TedToolkit.CppBindings.Generator",
                    "src/providers/cgal/TedToolkit.CppBindings.Cgal.Generator",
                    "externals/TedToolkit/props",
                    "src/tools/TedToolkit.CppBindings.Windows.Generation.Tool",
                ],
                [
                    "Build/CgalCompilerIdentity.cmake",
                    "Directory.Build.props",
                    "Directory.Build.targets",
                    "Directory.Packages.props",
                    "src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj",
                ],
                ["installed/x64-windows/include/CGAL",],
                [new("installed/vcpkg/info", "cgal_*.list"),],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CGAL.txt"] = "installed/x64-windows/share/cgal/copyright",
                    ["GMP.txt"] = "installed/x64-windows/share/gmp/copyright",
                    ["MPFR.txt"] = "installed/x64-windows/share/mpfr/copyright",
                }),
            "manifold" => new(
                "manifold",
                "Manifold",
                "ted_toolkit_cpp_bindings_manifold.dll",
                NativeBuildKind.VisualStudio,
                ToolchainProfileKind.Manifold,
                WriteManagedManifest: false,
                WriteOutputManifest: true,
                IncludeVcpkgPathInFingerprint: false,
                RequiredManagedFile: "Manifold.Bindings.g.cs",
                [
                    "src/shared/TedToolkit.CppBindings.Generator",
                    "src/providers/manifold/TedToolkit.CppBindings.Manifold.Generator",
                    "src/tools/TedToolkit.CppBindings.Windows.Generation.Tool",
                ],
                [
                    "Directory.Build.props",
                    "Directory.Build.targets",
                    "Directory.Packages.props",
                    "src/providers/manifold/TedToolkit.CppBindings.Manifold.Windows/TedToolkit.CppBindings.Manifold.Windows.csproj",
                ],
                ["installed/x64-windows/include/manifold",],
                [new("installed/vcpkg/info", "manifold_*.list"),],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Manifold.txt"] = "installed/x64-windows/share/manifold/copyright",
                    ["Clipper2.txt"] = "installed/x64-windows/share/clipper2/copyright",
                    ["TBB.txt"] = "installed/x64-windows/share/tbb/copyright",
                }),
            "fcl" => new(
                "fcl",
                "FCL",
                "ted_toolkit_cpp_bindings_fcl.dll",
                NativeBuildKind.VisualStudio,
                ToolchainProfileKind.Fcl,
                WriteManagedManifest: false,
                WriteOutputManifest: true,
                IncludeVcpkgPathInFingerprint: false,
                RequiredManagedFile: "Fcl.Bindings.g.cs",
                [
                    "src/shared/TedToolkit.CppBindings.Generator",
                    "src/providers/fcl/TedToolkit.CppBindings.Fcl.Generator",
                    "src/tools/TedToolkit.CppBindings.Windows.Generation.Tool",
                ],
                [
                    "Directory.Build.props",
                    "Directory.Build.targets",
                    "Directory.Packages.props",
                    "src/providers/fcl/TedToolkit.CppBindings.Fcl.Windows/TedToolkit.CppBindings.Fcl.Windows.csproj",
                ],
                ["installed/x64-windows/include/fcl",],
                [new("installed/vcpkg/info", "fcl_*.list"),],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FCL.txt"] = "installed/x64-windows/share/fcl/copyright",
                    ["libccd.txt"] = "installed/x64-windows/share/ccd/copyright",
                    ["Eigen.txt"] = "installed/x64-windows/share/eigen3/copyright",
                    ["Octomap.txt"] = "installed/x64-windows/share/octomap/copyright",
                }),
            _ => throw new ArgumentException($"Unknown Windows provider '{provider}'.", nameof(provider)),
        };
    }
}