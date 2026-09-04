// -----------------------------------------------------------------------
// <copyright file="NativeExportInventory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

namespace TedToolkit.CppBindings.Occt.Generator.Generators;

/// <summary>
/// Derives the OCCT provider's complete native export inventory from its finalized models.
/// </summary>
internal static class NativeExportInventory
{
    /// <summary>
    /// Gets every native function in deterministic function-table order.
    /// </summary>
    /// <param name="records">The completed declaration models.</param>
    /// <returns>The ordered unique native function names.</returns>
    /// <exception cref="InvalidOperationException">A native function name is duplicated.</exception>
    internal static string[] GetExports(IReadOnlyList<RecordModel> records)
    {
        var exports = new HashSet<string>(StringComparer.Ordinal) { "NativeError_Clear", };
        foreach (var record in records.OrderBy(static value => value.Type.CSharpTypeName, StringComparer.Ordinal))
        {
            foreach (var method in record.MethodModels)
            {
                if (!exports.Add(method.NativeExportName))
                {
                    throw new InvalidOperationException($"Duplicate native export '{method.NativeExportName}'.");
                }
            }

            foreach (var relation in record.Bases.Where(static value =>
                         value.IsPublic
                         && value.PointerAdjustment is PointerAdjustmentKind.NativeAdjust))
            {
                var export = NativeExportNameBuilder.GetPointerAdjustmentName(record, relation.Base);
                if (!exports.Add(export))
                {
                    throw new InvalidOperationException($"Duplicate native export '{export}'.");
                }
            }
        }

        return exports.Order(StringComparer.Ordinal).ToArray();
    }
}