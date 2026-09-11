// -----------------------------------------------------------------------
// <copyright file="NativeExportInventory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Derives a provider's complete native export inventory from its finalized models.
/// </summary>
public static class NativeExportInventory
{
    /// <summary>
    /// Gets every native function in deterministic function-table order.
    /// </summary>
    /// <param name="records">The completed declaration models.</param>
    /// <param name="providerExports">Provider-wide exports not owned by one declaration.</param>
    /// <returns>The ordered unique native function names.</returns>
    /// <exception cref="InvalidOperationException">A native function name is duplicated.</exception>
    public static string[] GetExports(
        IReadOnlyList<RecordModel> records,
        IEnumerable<string>? providerExports = null)
    {
        var exports = new HashSet<string>(providerExports ?? [], StringComparer.Ordinal);
        foreach (var record in records.OrderBy(static value => value.Type.CSharpTypeName, StringComparer.Ordinal))
        {
            foreach (var export in GetDeclarationExports(record))
            {
                if (!exports.Add(export))
                {
                    throw new InvalidOperationException($"Duplicate native export '{export}'.");
                }
            }
        }

        return exports.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Gets the native exports owned by one declaration, excluding provider-wide support exports.
    /// </summary>
    /// <param name="record">The completed declaration model.</param>
    /// <returns>The declaration export names before Shared assigns deterministic slots.</returns>
    public static string[] GetDeclarationExports(RecordModel record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.MethodModels.Select(static method => method.NativeExportName)
            .Concat(record.Bases
                .Where(static value => value.IsPublic
                                       && value.PointerAdjustment is PointerAdjustmentKind.NativeAdjust)
                .Select(relation => NativeExportNameBuilder.GetPointerAdjustmentName(record, relation.Base)))
            .ToArray();
    }
}