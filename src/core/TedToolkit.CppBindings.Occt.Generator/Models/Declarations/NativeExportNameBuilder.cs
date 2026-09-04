// -----------------------------------------------------------------------
// <copyright file="NativeExportNameBuilder.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Models.Declarations;

/// <summary>
/// Assigns deterministic native export names to one completed record.
/// </summary>
internal static class NativeExportNameBuilder
{
    /// <summary>
    /// Gets the canonical pointer-adjustment export name for one direct base relation.
    /// </summary>
    /// <param name="derived">The derived record.</param>
    /// <param name="baseRecord">The direct base record.</param>
    /// <returns>The native export name.</returns>
    public static string GetPointerAdjustmentName(RecordModel derived, RecordModel baseRecord)
    {
        return $"{ToIdentifier(derived.Type.CppTypeName)}_As_{ToIdentifier(baseRecord.Type.CppTypeName)}";
    }

    /// <summary>
    /// Gets the intrusive-release export required by an owning OCCT handle result.
    /// </summary>
    /// <param name="elementCppType">The native handle element type.</param>
    /// <returns>The release export name.</returns>
    public static string GetHandleReleaseName(string elementCppType)
    {
        ArgumentException.ThrowIfNullOrEmpty(elementCppType);
        return ToIdentifier(elementCppType) + "_Release";
    }

    /// <summary>
    /// Assigns every method export name once from declaration order.
    /// </summary>
    /// <param name="record">The completed record.</param>
    public static void Assign(RecordModel record)
    {
        ArgumentNullException.ThrowIfNull(record);
        foreach (var overloads in record.MethodModels.GroupBy(method => GetOperationName(record, method)))
        {
            var methods = overloads.ToArray();
            var handleRelease = methods.SingleOrDefault(static method =>
                method.Type is MethodModelType.HANDLE_RELEASE);
            var overloadIndex = 1;
            for (var index = 0; index < methods.Length; index++)
            {
                var name = ToIdentifier(record.Type.CppTypeName) + "_" + overloads.Key;
                if (ReferenceEquals(methods[index], handleRelease))
                {
                    methods[index].NativeExportName = name;
                    continue;
                }

                methods[index].NativeExportName = methods.Length is 1
                    ? name
                    : name + "_" + overloadIndex++;
            }
        }
    }

    private static string GetOperationName(RecordModel record, MethodModel method)
    {
        return method.Type is MethodModelType.HANDLE_RELEASE
               || (method.Type is MethodModelType.DELETE && record.IsStandardTransient)
            ? "Release"
            : method.GetNativeOperationName();
    }

    private static string ToIdentifier(string value)
    {
        return value.ToGeneratedTypeName();
    }
}