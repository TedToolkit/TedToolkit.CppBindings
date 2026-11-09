// -----------------------------------------------------------------------
// <copyright file="INativeSourceProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Manifold.Generator;
using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal interface INativeSourceProvider
{
    DirectoryPath Root { get; }

    string FingerprintIdentity { get; }

    IReadOnlyList<string> CMakeArguments { get; }

    string RuntimeDirectory { get; }

    IReadOnlyDictionary<string, string> Notices { get; }

    IEnumerable<string> EnumerateInputFiles();

    Task GenerateAsync(DirectoryPath output, CancellationToken cancellationToken);
}

internal sealed class VcpkgNativeSourceProvider(DirectoryPath root, ProviderDescriptor descriptor)
    : INativeSourceProvider
{
    public DirectoryPath Root { get; } = root.GetFullPath();

    public string FingerprintIdentity => descriptor.IncludeVcpkgPathInFingerprint
        ? $"vcpkg|{Root.FullName}"
        : "vcpkg";

    public IReadOnlyList<string> CMakeArguments =>
    [
        $"-DCMAKE_TOOLCHAIN_FILE={Path.Combine(Root.FullName, "scripts", "buildsystems", "vcpkg.cmake")}",
        "-DVCPKG_TARGET_TRIPLET=x64-windows",
        "-DVCPKG_APPLOCAL_DEPS=OFF",
    ];

    public string RuntimeDirectory => Path.Combine(Root.FullName, "installed", "x64-windows", "bin");

    public IReadOnlyDictionary<string, string> Notices => descriptor.Notices.ToDictionary(
        static notice => notice.Key,
        notice => Path.Combine(Root.FullName, notice.Value),
        StringComparer.Ordinal);

    public IEnumerable<string> EnumerateInputFiles()
    {
        var installRoot = Root.FullName;
        var headerFiles = descriptor.VcpkgInputRoots.SelectMany(relative =>
            EnumerateFiles(Path.Combine(installRoot, relative), "*", SearchOption.AllDirectories));
        var inventoryFiles = descriptor.VcpkgInputPatterns.SelectMany(pattern =>
            EnumerateFiles(Path.Combine(installRoot, pattern.RelativeRoot), pattern.SearchPattern,
                SearchOption.TopDirectoryOnly));
        return headerFiles.Concat(inventoryFiles)
            .Append(Path.Combine(installRoot, "installed", "vcpkg", "status"))
            .Where(File.Exists);
    }

    public Task GenerateAsync(DirectoryPath output, CancellationToken cancellationToken)
    {
        return ProviderGenerators.GenerateFromVcpkgAsync(descriptor.Id, output, Root, cancellationToken);
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern, SearchOption searchOption)
    {
        return Directory.Exists(root) ? Directory.EnumerateFiles(root, pattern, searchOption) : [];
    }
}

internal sealed class ManifoldInstallSourceProvider(DirectoryPath prefix) : INativeSourceProvider
{
    public DirectoryPath Root { get; } = prefix.GetFullPath();

    public string FingerprintIdentity => $"install-prefix|{Root.FullName}";

    public IReadOnlyList<string> CMakeArguments =>
    [
        $"-DCPPBINDINGS_MANIFOLD_PREFIX={Root.FullName}",
        $"-DCMAKE_PREFIX_PATH={Root.FullName}",
    ];

    public string RuntimeDirectory
    {
        get
        {
            var path = Path.Combine(Root.FullName, "bin");
            return Directory.Exists(path) ? path : string.Empty;
        }
    }

    public IReadOnlyDictionary<string, string> Notices => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Manifold.txt"] = Path.Combine(Root.FullName, "share", "manifold", "copyright"),
    };

    public IEnumerable<string> EnumerateInputFiles()
    {
        foreach (var relativePath in new[] { "include", "lib", "bin", "share" })
        {
            var directory = Path.Combine(Root.FullName, relativePath);
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
        }
    }

    public Task GenerateAsync(DirectoryPath output, CancellationToken cancellationToken)
    {
        return ProviderGenerators.GenerateManifoldAsync(
            output, ManifoldGenerationProvider.FromInstallation(Root), cancellationToken);
    }
}
