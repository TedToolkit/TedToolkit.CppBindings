// -----------------------------------------------------------------------
// <copyright file="IGeneratorService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using TedToolkit.Occt.Generator.Generators;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Creates generator instances for the supported output formats.
/// </summary>
public interface IGeneratorService
{
    /// <summary>
    /// Creates a C# generator for the specified record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The generator instance.</returns>
    CSharpGenerator GenerateCSharp(CXXRecordDecl record);

    /// <summary>
    /// Creates a C++ generator for the specified record.
    /// </summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The generator instance.</returns>
    CppGenerator GenerateCpp(CXXRecordDecl record);
}