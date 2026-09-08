// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationPlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>
/// Represents one immutable finite-profile generation plan.
/// </summary>
public sealed record ManifoldGenerationPlan
{
    /// <summary>Gets the finite profile identifier.</summary>
    public required string ProfileId { get; init; }

    /// <summary>Gets generated managed files keyed by relative path.</summary>
    public required IReadOnlyDictionary<string, string> ManagedSources { get; init; }

    /// <summary>Gets generated native files keyed by relative path.</summary>
    public required IReadOnlyDictionary<string, string> NativeSources { get; init; }

    /// <summary>Gets the exact ordered function-table symbol inventory.</summary>
    public required IReadOnlyList<string> NativeExports { get; init; }

    internal static IReadOnlyDictionary<string, string> Snapshot(IDictionary<string, string> values)
    {
        return new ReadOnlyDictionary<string, string>(
            new SortedDictionary<string, string>(values, StringComparer.Ordinal));
    }
}
