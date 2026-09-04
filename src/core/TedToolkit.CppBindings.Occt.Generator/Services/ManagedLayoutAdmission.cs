// -----------------------------------------------------------------------
// <copyright file="ManagedLayoutAdmission.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Closes the supported managed declaration graph before either language builds its callable inventory.
/// </summary>
internal static class ManagedLayoutAdmission
{
    /// <summary>
    /// Rejects unrepresentable alignment and affected typed members while retaining exact opaque storage.
    /// </summary>
    /// <param name="records">Records with completed native layout evidence.</param>
    /// <param name="diagnostics">Destination for native-identity-based rejection reasons.</param>
    /// <returns>The supported declaration set.</returns>
    internal static RecordModel[] Apply(RecordModel[] records, List<string> diagnostics)
    {
        var rejected = new HashSet<RecordModel>();
        foreach (var record in records.Where(static record => record.Alignment is not (1 or 2 or 4 or 8)))
        {
            _ = rejected.Add(record);
            diagnostics.Add($"record {record.Type.CppTypeName}: native alignment {record.Alignment} is not proved "
                + "by win-x64 sequential storage (supported: 1, 2, 4, 8); declaration and its operations omitted.");
        }

        CloseInheritance(records, rejected, diagnostics);
        var admitted = records.Where(record => !rejected.Contains(record)).ToArray();
        foreach (var record in admitted)
        {
            record.FieldModels = record.FieldModels.Where(field =>
                KeepMember(record, $"field {field.Name}", field.Type.ReferencedRecord, rejected, diagnostics)).ToArray();
            record.MethodModels = record.MethodModels.Where(method =>
                KeepOperation(record, method, rejected, diagnostics)).ToArray();
        }

        diagnostics.Sort(StringComparer.Ordinal);
        return admitted;
    }

    private static void CloseInheritance(
        RecordModel[] records,
        HashSet<RecordModel> rejected,
        List<string> diagnostics)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var record in records.Where(record => !rejected.Contains(record)))
            {
                var missingBase = record.Bases.FirstOrDefault(relation =>
                    relation.IsPublic && rejected.Contains(relation.Base));
                if (missingBase is null)
                {
                    continue;
                }

                changed |= rejected.Add(record);
                diagnostics.Add($"record {record.Type.CppTypeName}: required public base "
                    + $"{missingBase.Base.Type.CppTypeName} has no supported managed representation; "
                    + "declaration and its operations omitted.");
            }
        }
        while (changed);
    }

    private static bool KeepOperation(
        RecordModel record,
        MethodModel method,
        HashSet<RecordModel> rejected,
        List<string> diagnostics)
    {
        var dependency = method.Parameters.Select(static parameter => parameter.Type.ReferencedRecord)
            .Prepend(method.ReturnType.ReferencedRecord)
            .FirstOrDefault(candidate => candidate is not null && rejected.Contains(candidate));
        return KeepMember(record, $"operation {method.MethodName} ({method.NativeExportName})",
            dependency, rejected, diagnostics);
    }

    private static bool KeepMember(
        RecordModel record,
        string member,
        RecordModel? dependency,
        HashSet<RecordModel> rejected,
        List<string> diagnostics)
    {
        if (dependency is null || !rejected.Contains(dependency))
        {
            return true;
        }

        diagnostics.Add($"{member} in {record.Type.CppTypeName}: required type {dependency.Type.CppTypeName} "
            + "has no supported managed representation; member omitted.");
        return false;
    }
}