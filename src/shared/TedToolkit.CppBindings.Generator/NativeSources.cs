// -----------------------------------------------------------------------
// <copyright file="NativeSources.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Resolves native inputs from vcpkg installations, source trees, and explicit headers.
/// </summary>
public static class NativeSources
{
    /// <summary>
    /// Selects headers and the library directory from a populated vcpkg installation.
    /// </summary>
    /// <param name="installationRoot">The classic <c>installed</c> or manifest <c>vcpkg_installed</c> directory.</param>
    /// <param name="triplet">The installed triplet directory name.</param>
    /// <param name="publicHeaders">The entry headers relative to the triplet's include directory.</param>
    /// <returns>The selected installed inputs.</returns>
    /// <exception cref="ArgumentException"><paramref name="triplet"/> is not a single directory name.</exception>
    public static NativeSourceInputs FromVcpkg(
        in DirectoryPath installationRoot,
        string triplet,
        IEnumerable<string> publicHeaders)
    {
        ArgumentNullException.ThrowIfNull(installationRoot.FullName, nameof(installationRoot));
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        if (triplet is "." or ".."
            || triplet.Contains('/', StringComparison.Ordinal)
            || triplet.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(triplet))
        {
            throw new ArgumentException("A vcpkg triplet must be one directory name.", nameof(triplet));
        }

        var tripletRoot = installationRoot.GetFullPath() / triplet;
        var libraryDirectory = tripletRoot / "lib";
        return FromHeaders(
            tripletRoot / "include",
            publicHeaders,
            dependencyLibraryDirectories: libraryDirectory.Exists ? [libraryDirectory,] : []);
    }

    /// <summary>
    /// Selects a C++ source tree and entry headers for the binding provider.
    /// </summary>
    /// <param name="sourceDirectory">The project source directory.</param>
    /// <param name="includeSubdirectory">The include directory relative to the source root. Use <c>.</c> for the root.</param>
    /// <param name="publicHeaders">The entry headers relative to the include directory.</param>
    /// <param name="dependencyIncludeDirectories">Additional include directories, in search order.</param>
    /// <param name="dependencyLibraryDirectories">Directories in which to search for native libraries.</param>
    /// <param name="dependencyLibraries">Explicit native library files, in link order.</param>
    /// <returns>The selected source tree and dependencies.</returns>
    public static NativeSourceInputs FromSourceDirectory(
        in DirectoryPath sourceDirectory,
        string includeSubdirectory,
        IEnumerable<string> publicHeaders,
        IEnumerable<DirectoryPath>? dependencyIncludeDirectories = null,
        IEnumerable<DirectoryPath>? dependencyLibraryDirectories = null,
        IEnumerable<FilePath>? dependencyLibraries = null)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory.FullName, nameof(sourceDirectory));
        var root = sourceDirectory.GetFullPath();
        var includeRoot = new DirectoryPath(ResolveUnderRoot(root.FullName, includeSubdirectory));
        return Validate(new NativeSourceInputs(
            includeRoot, NormalizeHeaders(publicHeaders), dependencyIncludeDirectories, dependencyLibraryDirectories,
            dependencyLibraries, root));
    }

    /// <summary>
    /// Selects explicit entry headers and the paths required to compile and link against them.
    /// </summary>
    /// <param name="includeRoot">The include root of the entry headers.</param>
    /// <param name="publicHeaders">The entry headers relative to the include root.</param>
    /// <param name="dependencyIncludeDirectories">Additional include directories, in search order.</param>
    /// <param name="dependencyLibraryDirectories">Directories in which to search for native libraries.</param>
    /// <param name="dependencyLibraries">Explicit native library files, in link order.</param>
    /// <returns>The selected headers and dependencies.</returns>
    public static NativeSourceInputs FromHeaders(
        in DirectoryPath includeRoot,
        IEnumerable<string> publicHeaders,
        IEnumerable<DirectoryPath>? dependencyIncludeDirectories = null,
        IEnumerable<DirectoryPath>? dependencyLibraryDirectories = null,
        IEnumerable<FilePath>? dependencyLibraries = null)
    {
        return Validate(new NativeSourceInputs(
            includeRoot, NormalizeHeaders(publicHeaders), dependencyIncludeDirectories, dependencyLibraryDirectories,
            dependencyLibraries));
    }

    private static IEnumerable<string> NormalizeHeaders(IEnumerable<string> publicHeaders)
    {
        ArgumentNullException.ThrowIfNull(publicHeaders);
        return publicHeaders.Select(static header =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(header);
            return header.Replace('\\', '/');
        });
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var path = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(path) || path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new ArgumentException("Expected a relative path beneath the source directory.", nameof(relativePath));
        }

        return Path.GetFullPath(path, root);
    }

    private static void RequireDirectory(in DirectoryPath directory)
    {
        if (directory.Exists)
        {
            return;
        }

        throw new DirectoryNotFoundException($"Native source directory '{directory.FullName}' was not found.");
    }

    private static NativeSourceInputs Validate(NativeSourceInputs inputs)
    {
        RequireDirectory(inputs.IncludeRoot);
        if (inputs.SourceDirectory is { } sourceDirectory)
        {
            RequireDirectory(sourceDirectory);
        }

        foreach (var directory in inputs.DependencyIncludeDirectories.Concat(inputs.DependencyLibraryDirectories))
        {
            RequireDirectory(directory);
        }

        foreach (var header in inputs.PublicHeaders)
        {
            var path = ResolveUnderRoot(inputs.IncludeRoot.FullName, header);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Native entry header was not found.", path);
            }
        }

        foreach (var library in inputs.DependencyLibraries)
        {
            if (!library.Exists)
            {
                throw new FileNotFoundException("Native dependency library was not found.", library.FullName);
            }
        }

        return inputs;
    }
}