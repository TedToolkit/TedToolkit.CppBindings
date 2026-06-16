// -----------------------------------------------------------------------
// <copyright file="ITypeService.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Occt.Generator.Services.Interfaces;

/// <summary>
/// Provides name and type normalization for Clang types.
/// </summary>
public interface ITypeService
{
    /// <summary>
    /// Gets the C++ display name for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The C++ type name.</returns>
    string GetCppName(ClangSharp.Type type);

    /// <summary>
    /// Gets the generated C# identifier for a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The C# type name.</returns>
    string GetCSharpName(ClangSharp.Type type);

    /// <summary>
    /// Removes type sugar from a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The desugared type.</returns>
    ClangSharp.Type DesugarType(ClangSharp.Type type);
}