// -----------------------------------------------------------------------
// <copyright file="NativeSourceInputs.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Describes the source tree, public headers, and native dependencies for a binding provider.
/// </summary>
public sealed class NativeSourceInputs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeSourceInputs"/> class.
    /// </summary>
    /// <param name="includeRoot">The include root that resolves the public header paths.</param>
    /// <param name="publicHeaders">The include-relative paths in deterministic order.</param>
    /// <param name="dependencyIncludeDirectories">Additional include directories, in search order.</param>
    /// <param name="dependencyLibraryDirectories">Directories in which to search for native libraries.</param>
    /// <param name="dependencyLibraries">Explicit native library files, in link order.</param>
    /// <param name="sourceDirectory">The project source directory, when the source is available.</param>
    public NativeSourceInputs(
        in DirectoryPath includeRoot,
        IEnumerable<string> publicHeaders,
        IEnumerable<DirectoryPath>? dependencyIncludeDirectories = null,
        IEnumerable<DirectoryPath>? dependencyLibraryDirectories = null,
        IEnumerable<FilePath>? dependencyLibraries = null,
        DirectoryPath? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(includeRoot.FullName, nameof(includeRoot));
        ArgumentNullException.ThrowIfNull(publicHeaders);
        IncludeRoot = includeRoot.GetFullPath();
        PublicHeaders = Array.AsReadOnly(publicHeaders.ToArray());
        SourceDirectory = sourceDirectory?.GetFullPath();
        DependencyIncludeDirectories = SnapshotDirectories(dependencyIncludeDirectories);
        DependencyLibraryDirectories = SnapshotDirectories(dependencyLibraryDirectories);
        DependencyLibraries = Array.AsReadOnly((dependencyLibraries ?? [])
            .Select(static path => path.GetFullPath()).ToArray());
    }

    /// <summary>
    /// Gets the absolute include root.
    /// </summary>
    public DirectoryPath IncludeRoot { get; }

    /// <summary>
    /// Gets the public header paths relative to the include root.
    /// </summary>
    public IReadOnlyList<string> PublicHeaders { get; }

    /// <summary>
    /// Gets the absolute project source directory when the input includes C++ source.
    /// </summary>
    public DirectoryPath? SourceDirectory { get; }

    /// <summary>
    /// Gets the additional absolute include directories in search order.
    /// </summary>
    public IReadOnlyList<DirectoryPath> DependencyIncludeDirectories { get; }

    /// <summary>
    /// Gets the absolute directories in which to search for native libraries.
    /// </summary>
    public IReadOnlyList<DirectoryPath> DependencyLibraryDirectories { get; }

    /// <summary>
    /// Gets the explicit absolute native library files in link order.
    /// </summary>
    public IReadOnlyList<FilePath> DependencyLibraries { get; }

    private static ReadOnlyCollection<DirectoryPath> SnapshotDirectories(IEnumerable<DirectoryPath>? paths)
    {
        return Array.AsReadOnly((paths ?? []).Select(static path => path.GetFullPath()).ToArray());
    }
}