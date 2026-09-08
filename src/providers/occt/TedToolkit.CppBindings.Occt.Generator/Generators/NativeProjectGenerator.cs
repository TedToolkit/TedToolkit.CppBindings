// -----------------------------------------------------------------------
// <copyright file="NativeProjectGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Generates the native project for the complete emitted source inventory.
/// </summary>
internal static class NativeProjectGenerator
{
    private const int UnityBatchSize = 32;

    /// <summary>
    /// Gets the generated project file name.
    /// </summary>
    public const string FileName = "CMakeLists.txt";

    /// <summary>
    /// Generates a CMake project for the emitted native library.
    /// </summary>
    /// <param name="sourceFiles">The deterministic source file inventory.</param>
    /// <param name="libraryBaseName">The native library basename.</param>
    /// <param name="cppVersion">The C++ standard used for parsing and native compilation.</param>
    /// <returns>The generated CMake source.</returns>
    public static string Generate(IReadOnlyList<string> sourceFiles, string libraryBaseName, int cppVersion = 17)
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

        _ = builder.Append(")\n\n");
        AppendUnityGroups(builder, sourceFiles);
        _ = builder.Append("target_compile_features(").Append(libraryBaseName)
            .Append(" PRIVATE cxx_std_").Append(cppVersion).Append(")\n")
            .Append("target_compile_options(").Append(libraryBaseName)
            .Append(" PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/MP1>)\n")
            .Append("set_target_properties(").Append(libraryBaseName)
            .Append(" PROPERTIES\n")
            .Append("    UNITY_BUILD ON\n")
            .Append("    UNITY_BUILD_MODE GROUP\n")
            .Append("    RUNTIME_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}/$<CONFIG>\")\n")
            .Append("target_include_directories(").Append(libraryBaseName)
            .Append(" PRIVATE ${OpenCASCADE_INCLUDE_DIR})\n")
            .Append("target_link_libraries(").Append(libraryBaseName)
            .Append(" PRIVATE ${OpenCASCADE_LIBRARIES})\n");
        return builder.ToString();
    }

    private static void AppendUnityGroups(StringBuilder builder, IReadOnlyList<string> sourceFiles)
    {
        var orderedGroups = sourceFiles
            .Order(StringComparer.Ordinal)
            .GroupBy(static source =>
            {
                var stem = Path.GetFileNameWithoutExtension(source);
                var separator = stem.IndexOf('_', StringComparison.Ordinal);
                var family = separator < 0 ? stem : stem[..separator];
                var depth = stem.Count(static character => character == '_');
                return (Family: family, Depth: depth);
            });
        foreach (var familyDepthGroup in orderedGroups)
        {
            var batchIndex = 0;
            foreach (var batch in familyDepthGroup.Chunk(UnityBatchSize))
            {
                _ = builder.Append("set_source_files_properties(\n");
                foreach (var source in batch)
                {
                    _ = builder.Append("    ").Append(source).Append('\n');
                }

                _ = builder.Append("    PROPERTIES UNITY_GROUP \"").Append(familyDepthGroup.Key.Family)
                    .Append("_depth_").Append(familyDepthGroup.Key.Depth)
                    .Append("_batch_").Append(batchIndex).Append("\")\n\n");
                batchIndex++;
            }
        }
    }
}