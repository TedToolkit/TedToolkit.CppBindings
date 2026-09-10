// -----------------------------------------------------------------------
// <copyright file="BindingCMakeProjectDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes a provider-neutral CMake project from native dependency and target facts.
/// </summary>
public sealed class BindingCMakeProjectDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingCMakeProjectDefinition"/> class.
    /// </summary>
    /// <param name="projectName">The CMake project name.</param>
    /// <param name="libraryBaseName">The native library basename.</param>
    /// <param name="cppVersion">The required C++ language version.</param>
    /// <param name="packages">The required CMake packages.</param>
    /// <param name="compileDefinitions">The target compile definitions.</param>
    /// <param name="compileOptions">The target compile options.</param>
    /// <param name="includeDirectories">The target include directories.</param>
    /// <param name="linkLibraries">The target link libraries.</param>
    /// <param name="targetProperties">The target properties.</param>
    /// <param name="unityBuild">The optional bounded unity-build policy.</param>
    /// <param name="relativePath">The output-relative project path.</param>
    /// <param name="minimumVersion">The minimum CMake version.</param>
    public BindingCMakeProjectDefinition(
        string projectName,
        string libraryBaseName,
        int cppVersion,
        IEnumerable<BindingCMakePackageDefinition> packages,
        IEnumerable<string>? compileDefinitions = null,
        IEnumerable<string>? compileOptions = null,
        IEnumerable<string>? includeDirectories = null,
        IEnumerable<string>? linkLibraries = null,
        IEnumerable<BindingCMakeTargetProperty>? targetProperties = null,
        BindingCMakeUnityBuildDefinition? unityBuild = null,
        string relativePath = "CMakeLists.txt",
        string minimumVersion = "3.28")
    {
        ProjectName = ValidateLine(projectName, nameof(projectName));
        LibraryBaseName = ValidateLine(libraryBaseName, nameof(libraryBaseName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cppVersion);
        CppVersion = cppVersion;
        MinimumVersion = ValidateLine(minimumVersion, nameof(minimumVersion));
        RelativePath = ValidateLine(relativePath, nameof(relativePath));
        Packages = Snapshot(packages, nameof(packages));
        foreach (var package in Packages)
        {
            _ = ValidateLine(package.Name, nameof(packages));
            _ = ValidateLine(package.Arguments, nameof(packages));
        }

        if (Packages.Select(static package => package.Name).Distinct(StringComparer.Ordinal).Count() != Packages.Count)
        {
            throw new ArgumentException("CMake package names must be unique.", nameof(packages));
        }

        CompileDefinitions = SnapshotLines(compileDefinitions, nameof(compileDefinitions));
        CompileOptions = SnapshotLines(compileOptions, nameof(compileOptions));
        IncludeDirectories = SnapshotLines(includeDirectories, nameof(includeDirectories));
        LinkLibraries = SnapshotLines(linkLibraries, nameof(linkLibraries));
        TargetProperties = Snapshot(targetProperties ?? [], nameof(targetProperties));
        foreach (var property in TargetProperties)
        {
            _ = ValidateLine(property.Name, nameof(targetProperties));
            _ = ValidateLine(property.Value, nameof(targetProperties));
        }

        if (TargetProperties.Select(static property => property.Name)
            .Distinct(StringComparer.Ordinal).Count() != TargetProperties.Count)
        {
            throw new ArgumentException("CMake target property names must be unique.", nameof(targetProperties));
        }

        UnityBuild = unityBuild is null
            ? null
            : new BindingCMakeUnityBuildDefinition(unityBuild.BatchSize);
    }

    /// <summary>
    /// Gets the CMake project name.
    /// </summary>
    public string ProjectName { get; }

    /// <summary>
    /// Gets the emitted native library basename.
    /// </summary>
    public string LibraryBaseName { get; }

    /// <summary>
    /// Gets the required C++ language version.
    /// </summary>
    public int CppVersion { get; }

    /// <summary>
    /// Gets the required native packages.
    /// </summary>
    public IReadOnlyList<BindingCMakePackageDefinition> Packages { get; }

    /// <summary>
    /// Gets the target compile definitions.
    /// </summary>
    public IReadOnlyList<string> CompileDefinitions { get; }

    /// <summary>
    /// Gets the target compile options.
    /// </summary>
    public IReadOnlyList<string> CompileOptions { get; }

    /// <summary>
    /// Gets the target include directories.
    /// </summary>
    public IReadOnlyList<string> IncludeDirectories { get; }

    /// <summary>
    /// Gets the target link libraries.
    /// </summary>
    public IReadOnlyList<string> LinkLibraries { get; }

    /// <summary>
    /// Gets the target properties.
    /// </summary>
    public IReadOnlyList<BindingCMakeTargetProperty> TargetProperties { get; }

    /// <summary>
    /// Gets the optional bounded unity-build policy.
    /// </summary>
    public BindingCMakeUnityBuildDefinition? UnityBuild { get; }

    /// <summary>
    /// Gets the output-relative project path.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the minimum CMake version.
    /// </summary>
    public string MinimumVersion { get; }

    private static System.Collections.ObjectModel.ReadOnlyCollection<T> Snapshot<T>(
        IEnumerable<T> values,
        string paramName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        var snapshot = values.ToArray();
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("CMake project collections cannot contain null values.", paramName);
        }

        return Array.AsReadOnly(snapshot);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<string> SnapshotLines(
        IEnumerable<string>? values,
        string paramName)
    {
        var snapshot = (values ?? []).Select(value => ValidateLine(value, paramName)).ToArray();
        return Array.AsReadOnly(snapshot);
    }

    private static string ValidateLine(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!value.Contains('\n', StringComparison.Ordinal) && !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        throw new ArgumentException("CMake facts must occupy one line.", paramName);
    }
}