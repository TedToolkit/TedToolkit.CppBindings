// -----------------------------------------------------------------------
// <copyright file="ManagedHandleCycles.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Identifies direct handle fields in cross-record managed storage cycles.
/// </summary>
internal static class ManagedHandleCycles
{
    /// <summary>
    /// Marks only handle edges whose pointee storage leads back to the containing record.
    /// </summary>
    /// <param name="records">The admitted native record graph.</param>
    internal static void Mark(RecordModel[] records)
    {
        var graph = records.ToDictionary(static record => record, static record => record.FieldModels
            .Where(static field => field.Type.Transport.Indirections.Count is 0)
            .Select(static field => field.Type.ReferencedRecord)
            .OfType<RecordModel>().Distinct().ToArray());
        foreach (var record in records)
        {
            foreach (var field in record.FieldModels.Where(static field =>
                         field.Type.IsIntrusiveHandle && field.Type.Transport.Indirections.Count is 0))
            {
                var target = field.Type.ReferencedRecord;
                field.UsesIntrusiveHandleReferenceStorage = target is not null && !ReferenceEquals(target, record)
                    && Reaches(graph, target, record);
            }
        }
    }

    private static bool Reaches(Dictionary<RecordModel, RecordModel[]> graph, RecordModel start, RecordModel target)
    {
        var pending = new Stack<RecordModel>();
        var visited = new HashSet<RecordModel>();
        pending.Push(start);
        while (pending.TryPop(out var current))
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            if (!visited.Add(current) || !graph.TryGetValue(current, out var edges))
            {
                continue;
            }

            foreach (var edge in edges)
            {
                pending.Push(edge);
            }
        }

        return false;
    }
}