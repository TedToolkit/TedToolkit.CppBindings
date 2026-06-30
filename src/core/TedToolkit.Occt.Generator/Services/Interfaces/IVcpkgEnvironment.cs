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
internal interface IVcpkgEnvironment
{
    /// <summary>
    /// Gets the vcpkg root folder.
    /// </summary>
    /// <returns>The vcpkg root folder.</returns>
    string GetRoot();

    /// <summary>
    /// Gets the vcpkg include folder.
    /// </summary>
    /// <param name="triplet">The target vcpkg triplet.</param>
    /// <returns>The include folder.</returns>
    string GetIncludeFolder(string triplet);

    /// <summary>
    /// Gets the OCCT include folder.
    /// </summary>
    /// <param name="triplet">The target vcpkg triplet.</param>
    /// <returns>The OCCT include folder.</returns>
    string GetOcctIncludeFolder(string triplet);

    /// <summary>
    /// Builds the aggregate OCCT include header content for the specified triplet.
    /// </summary>
    /// <param name="triplet">The target vcpkg triplet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The include header content.</returns>
    Task<string> GetIncludingHeaderContentAsync(string triplet, CancellationToken cancellationToken);
}