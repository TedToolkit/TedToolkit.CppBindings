// -----------------------------------------------------------------------
// <copyright file="IGenerationOutputCleaner.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Clears generator output folders while preserving the configured root directories.
/// </summary>
internal interface IGenerationOutputCleaner
{
    /// <summary>
    /// Empties the configured C# and C++ output folders before generation starts.
    /// </summary>
    void Clean();
}