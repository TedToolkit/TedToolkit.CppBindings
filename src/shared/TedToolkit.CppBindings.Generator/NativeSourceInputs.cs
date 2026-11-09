// -----------------------------------------------------------------------
// <copyright file="NativeSourceInputs.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Describes an include tree and the public headers selected from that tree.
/// </summary>
public sealed class NativeSourceInputs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeSourceInputs"/> class.
    /// </summary>
    /// <param name="includeRoot">The include root that resolves the public header paths.</param>
    /// <param name="publicHeaders">The include-relative paths in deterministic order.</param>
    public NativeSourceInputs(in DirectoryPath includeRoot, IEnumerable<string> publicHeaders)
    {
        ArgumentNullException.ThrowIfNull(includeRoot.FullName, nameof(includeRoot));
        ArgumentNullException.ThrowIfNull(publicHeaders);
        IncludeRoot = includeRoot.GetFullPath();
        PublicHeaders = Array.AsReadOnly(publicHeaders.ToArray());
    }

    /// <summary>
    /// Gets the absolute include root.
    /// </summary>
    public DirectoryPath IncludeRoot { get; }

    /// <summary>
    /// Gets the public header paths relative to the include root.
    /// </summary>
    public IReadOnlyList<string> PublicHeaders { get; }
}