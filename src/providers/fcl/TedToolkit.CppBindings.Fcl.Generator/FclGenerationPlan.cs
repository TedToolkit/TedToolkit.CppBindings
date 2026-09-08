// -----------------------------------------------------------------------
// <copyright file="FclGenerationPlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Contains one immutable FCL generation result.</summary>
public sealed record FclGenerationPlan
{
    /// <summary>Gets the profile identity.</summary>
    public required string ProfileId { get; init; }

    /// <summary>Gets the managed artifacts.</summary>
    public required IReadOnlyDictionary<string, string> ManagedSources { get; init; }

    /// <summary>Gets the native artifacts.</summary>
    public required IReadOnlyDictionary<string, string> NativeSources { get; init; }

    /// <summary>Gets the private function-table entries.</summary>
    public required IReadOnlyList<string> NativeFunctions { get; init; }

    internal static IReadOnlyDictionary<string, string> Snapshot(IDictionary<string, string> values)
    {
        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values, StringComparer.Ordinal));
    }
}
