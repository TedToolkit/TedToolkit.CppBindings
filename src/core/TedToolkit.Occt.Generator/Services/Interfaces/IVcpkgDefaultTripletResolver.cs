// -----------------------------------------------------------------------
// <copyright file="IVcpkgDefaultTripletResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Resolves the default vcpkg triplet from the local vcpkg/OCCT environment.
/// </summary>
public interface IVcpkgDefaultTripletResolver
{
    /// <summary>
    /// Gets the active vcpkg triplet when the user did not provide one.
    /// </summary>
    /// <returns>The triplet name.</returns>
    string GetTriplet();

}
