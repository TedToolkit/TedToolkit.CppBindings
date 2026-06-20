// -----------------------------------------------------------------------
// <copyright file="IVcpkgService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Resolves the local vcpkg installation and OCCT metadata.
/// </summary>
public interface IVcpkgService
{
    Task<string> IncludingHeaderContent(CancellationToken cancellationToken);
    /// <summary>
    /// Gets the vcpkg root folder.
    /// </summary>
    /// <returns>The vcpkg root folder.</returns>
    string GetRoot();

    /// <summary>
    /// Gets the vcpkg include folder.
    /// </summary>
    /// <returns>The include folder.</returns>
    string GetIncludeFolder();

    /// <summary>
    /// Gets the OCCT include folder.
    /// </summary>
    /// <returns>The OCCT include folder.</returns>
    string GetOcctIncludeFolder();

    /// <summary>
    /// Gets the active vcpkg triplet.
    /// </summary>
    /// <returns>The triplet name.</returns>
    string GetTriplet();

    /// <summary>
    /// Gets the OCCT C++ standard version.
    /// </summary>
    /// <returns>The C++ standard version.</returns>
    Task<int> GetOcctCppVersionAsync();
}