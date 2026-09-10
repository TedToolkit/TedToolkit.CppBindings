// -----------------------------------------------------------------------
// <copyright file="BindingCMakeProjectEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Emits deterministic CMake projects from provider-neutral project facts and Shared's source inventory.
/// </summary>
public static class BindingCMakeProjectEmitter
{
    /// <summary>
    /// Creates a native-project plan entry backed by this emitter.
    /// </summary>
    /// <param name="definition">The snapshotted project facts.</param>
    /// <returns>The native project consumed by Shared plan construction.</returns>
    public static BindingNativeProject CreateNativeProject(BindingCMakeProjectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(
            definition.RelativePath,
            (sources, writer, token) => writer.WriteAsync(Render(definition, sources).AsMemory(), token));
    }

    /// <summary>
    /// Renders a complete CMake project.
    /// </summary>
    /// <param name="definition">The project facts.</param>
    /// <param name="sourceFiles">The exact compiled native source inventory.</param>
    /// <returns>The generated CMake source.</returns>
    public static string Render(
        BindingCMakeProjectDefinition definition,
        IReadOnlyList<string> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var sources = ValidateSources(sourceFiles);
        var builder = new StringBuilder();
        _ = builder.Append("cmake_minimum_required(VERSION ").Append(definition.MinimumVersion).Append(")\n")
            .Append("project(").Append(definition.ProjectName).Append(" LANGUAGES CXX)\n\n");
        foreach (var package in definition.Packages.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            ValidateFact(package.Name, nameof(definition));
            ValidateFact(package.Arguments, nameof(definition));
            _ = builder.Append("find_package(").Append(package.Name).Append(' ')
                .Append(package.Arguments).Append(")\n");
        }

        if (definition.Packages.Count > 0)
        {
            _ = builder.Append('\n');
        }

        _ = builder.Append("add_library(").Append(definition.LibraryBaseName).Append(" SHARED\n");
        foreach (var source in sources)
        {
            _ = builder.Append("    ").Append(source).Append('\n');
        }

        _ = builder.Append(")\n");
        if (definition.UnityBuild is { } unityBuild)
        {
            _ = builder.Append('\n');
            AppendUnityGroups(builder, sources, unityBuild.BatchSize);
        }

        AppendTargetCommand(
            builder,
            "target_compile_features",
            definition.LibraryBaseName,
            ["cxx_std_" + definition.CppVersion,]);
        AppendTargetCommand(builder, "target_compile_definitions", definition.LibraryBaseName,
            definition.CompileDefinitions);
        AppendTargetCommand(builder, "target_compile_options", definition.LibraryBaseName,
            definition.CompileOptions);
        AppendTargetProperties(builder, definition);
        AppendTargetCommand(builder, "target_include_directories", definition.LibraryBaseName,
            definition.IncludeDirectories);
        AppendTargetCommand(builder, "target_link_libraries", definition.LibraryBaseName,
            definition.LinkLibraries);
        return builder.ToString();
    }

    private static string[] ValidateSources(IReadOnlyList<string> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        var sources = sourceFiles.ToArray();
        foreach (var source in sources)
        {
            ValidateFact(source, nameof(sourceFiles));
            var segments = source.Replace('\\', '/').Split('/');
            if (Path.IsPathFullyQualified(source)
                || segments.Any(static segment => segment is "." or "..")
                || !source.EndsWith(".cpp", StringComparison.Ordinal))
            {
                throw new ArgumentException("Native project sources must be relative .cpp paths.", nameof(sourceFiles));
            }
        }

        if (sources.Distinct(StringComparer.Ordinal).Count() != sources.Length)
        {
            throw new ArgumentException("Native project sources must be unique.", nameof(sourceFiles));
        }

        Array.Sort(sources, StringComparer.Ordinal);
        return sources;
    }

    private static void AppendUnityGroups(StringBuilder builder, IReadOnlyList<string> sourceFiles, int batchSize)
    {
        var groups = sourceFiles.GroupBy(static source =>
        {
            var stem = Path.GetFileNameWithoutExtension(source);
            var separator = stem.IndexOf('_', StringComparison.Ordinal);
            var family = separator < 0 ? stem : stem[..separator];
            var depth = stem.Count(static character => character == '_');
            return (Family: family, Depth: depth);
        });
        foreach (var group in groups)
        {
            var batchIndex = 0;
            foreach (var batch in group.Chunk(batchSize))
            {
                _ = builder.Append("set_source_files_properties(\n");
                foreach (var source in batch)
                {
                    _ = builder.Append("    ").Append(source).Append('\n');
                }

                _ = builder.Append("    PROPERTIES UNITY_GROUP \"").Append(group.Key.Family)
                    .Append("_depth_").Append(group.Key.Depth)
                    .Append("_batch_").Append(batchIndex).Append("\")\n\n");
                batchIndex++;
            }
        }
    }

    private static void AppendTargetCommand(
        StringBuilder builder,
        string command,
        string target,
        IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        _ = builder.Append(command).Append('(').Append(target).Append(" PRIVATE ")
            .AppendJoin(' ', values).Append(")\n");
    }

    private static void AppendTargetProperties(StringBuilder builder, BindingCMakeProjectDefinition definition)
    {
        if (definition.TargetProperties.Count == 0)
        {
            return;
        }

        _ = builder.Append("set_target_properties(").Append(definition.LibraryBaseName).Append(" PROPERTIES\n");
        foreach (var property in definition.TargetProperties)
        {
            ValidateFact(property.Name, nameof(definition));
            ValidateFact(property.Value, nameof(definition));
            _ = builder.Append("    ").Append(property.Name).Append(' ').Append(property.Value).Append('\n');
        }

        _ = builder.Append(")\n");
    }

    private static void ValidateFact(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!value.Contains('\n', StringComparison.Ordinal) && !value.Contains('\r', StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException("CMake facts must occupy one line.", paramName);
    }
}