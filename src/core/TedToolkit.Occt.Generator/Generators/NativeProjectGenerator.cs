// -----------------------------------------------------------------------
// <copyright file="NativeProjectGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Generates the native project for the complete emitted source inventory.
/// </summary>
internal static class NativeProjectGenerator
{
    /// <summary>
    /// Gets the generated project file name.
    /// </summary>
    public const string FileName = "CMakeLists.txt";

    /// <summary>
    /// Generates a CMake project for the emitted native library.
    /// </summary>
    /// <param name="sourceFiles">The deterministic source file inventory.</param>
    /// <param name="libraryBaseName">The native library basename.</param>
    /// <returns>The generated CMake source.</returns>
    public static string Generate(IReadOnlyList<string> sourceFiles, string libraryBaseName)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentException.ThrowIfNullOrEmpty(libraryBaseName);

        var builder = new StringBuilder(
            "cmake_minimum_required(VERSION 3.28)\n"
            + "project(TedToolkitOcctGenerated LANGUAGES CXX)\n\n"
            + "find_package(OpenCASCADE CONFIG REQUIRED)\n\n"
            + "add_library(");
        _ = builder.Append(libraryBaseName).Append(" SHARED\n");
        foreach (var source in sourceFiles.Order(StringComparer.Ordinal))
        {
            _ = builder.Append("    ").Append(source).Append('\n');
        }

        _ = builder.Append(")\n\ntarget_compile_features(").Append(libraryBaseName)
            .Append(" PRIVATE cxx_std_17)\n")
            .Append("target_include_directories(").Append(libraryBaseName)
            .Append(" PRIVATE ${OpenCASCADE_INCLUDE_DIR})\n")
            .Append("target_link_libraries(").Append(libraryBaseName)
            .Append(" PRIVATE ${OpenCASCADE_LIBRARIES})\n")
            .Append("set_target_properties(").Append(libraryBaseName)
            .Append(" PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)\n");
        return builder.ToString();
    }
}