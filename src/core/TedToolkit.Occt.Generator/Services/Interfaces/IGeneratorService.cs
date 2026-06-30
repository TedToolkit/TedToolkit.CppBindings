// -----------------------------------------------------------------------
// <copyright file="IGeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Creates generator instances for the supported output formats.
/// </summary>
internal interface IGeneratorService
{
    /// <summary>
    /// Creates a C# generator for the specified record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The generator instance.</returns>
    IGenerator GenerateCSharp(RecordModel record);

    /// <summary>
    /// Creates a C++ generator for the specified record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The generator instance.</returns>
    IGenerator GenerateCpp(RecordModel record);

    /// <summary>
    /// Creates a C# generator for the specified enum.
    /// </summary>
    /// <param name="enumModel">The enum declaration.</param>
    /// <returns>The generator instance.</returns>
    IGenerator GenerateCSharp(EnumModel enumModel);
}