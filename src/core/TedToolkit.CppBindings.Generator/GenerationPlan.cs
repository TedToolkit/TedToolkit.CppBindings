// -----------------------------------------------------------------------
// <copyright file="GenerationPlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Holds immutable source inventories and the exact shared native export order for one run.
/// </summary>
public sealed class GenerationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationPlan"/> class with copied inventories.
    /// </summary>
    /// <param name="cSharpSources">Provider-owned managed sources.</param>
    /// <param name="cppSources">Provider-owned native sources and build descriptions.</param>
    /// <param name="nativeExports">Exact C identifier ordering consumed by both binding sides.</param>
    public GenerationPlan(
        IEnumerable<GeneratedSource> cSharpSources,
        IEnumerable<GeneratedSource> cppSources,
        IEnumerable<string> nativeExports)
    {
        ArgumentNullException.ThrowIfNull(cSharpSources);
        ArgumentNullException.ThrowIfNull(cppSources);
        ArgumentNullException.ThrowIfNull(nativeExports);
        CSharpSources = Array.AsReadOnly(cSharpSources.ToArray());
        CppSources = Array.AsReadOnly(cppSources.ToArray());
        NativeExports = Array.AsReadOnly(nativeExports.ToArray());
    }

    /// <summary>
    /// Gets the managed source snapshot, excluding the core-owned NativeApi.g.cs loader.
    /// </summary>
    public IReadOnlyList<GeneratedSource> CSharpSources { get; }

    /// <summary>
    /// Gets the native source snapshot, excluding the core-owned NativeFunctionTable.cpp.
    /// </summary>
    public IReadOnlyList<GeneratedSource> CppSources { get; }

    /// <summary>
    /// Gets the exact ordered native function names shared by provider renderers and the function table.
    /// </summary>
    public IReadOnlyList<string> NativeExports { get; }
}