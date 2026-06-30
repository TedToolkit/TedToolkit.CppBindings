// -----------------------------------------------------------------------
// <copyright file="IVcpkgEnvironment.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides access to the local vcpkg installation and OCCT metadata.
/// </summary>
public interface IVcpkgEnvironment
{
    /// <summary>
     /// Gets the vcpkg root folder.
    /// </summary>
    /// <returns>The vcpkg root folder.</returns>
    string GetRoot();

    /// <summary>
    /// Gets the vcpkg include folder.
    /// </summary>
    /// <returns>The include folder.</returns>
    string GetIncludeFolder(string triplet);

    /// <summary>
    /// Gets the OCCT include folder.
    /// </summary>
    /// <returns>The OCCT include folder.</returns>
    string GetOcctIncludeFolder(string triplet);

    Task<string> IncludingHeaderContent(string triplet, CancellationToken cancellationToken);
}
