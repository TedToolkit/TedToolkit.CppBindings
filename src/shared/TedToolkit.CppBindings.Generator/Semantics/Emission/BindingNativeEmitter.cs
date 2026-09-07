// -----------------------------------------------------------------------
// <copyright file="BindingNativeEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Generates the C-linkage exports for one parsed record.
/// </summary>
/// <remarks>
/// Each generated source owns exactly one record and declares no C++ namespace.
/// </remarks>
/// <param name="record">The normalized record that remains the declaration authority.</param>
/// <param name="emissionProfile">The provider's finite emission policy.</param>
public sealed class BindingNativeEmitter(RecordModel record, BindingEmissionProfile emissionProfile) : IBindingSourceEmitter
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (record.ObjectKind is NativeObjectKind.Unknown)
        {
            throw new InvalidOperationException(
                $"Native compiler facts were not completed for '{record.Type.CppTypeName}'.");
        }

        if (record.MethodModels.Any(static method => string.IsNullOrEmpty(method.NativeExportName)))
        {
            throw new InvalidOperationException(
                $"Native export names were not completed for '{record.Type.CppTypeName}'.");
        }

        var hasThrowingMethods = record.MethodModels.Any(static method => !method.NoExceptions);
        var builder = new StringBuilder(emissionProfile.NativePreamble);
        if (hasThrowingMethods)
        {
            _ = builder.Append(emissionProfile.NativeErrorPreamble);
        }

        foreach (var header in GetRequiredHeaders(record))
        {
            _ = builder.Append("#include <").Append(header).Append(">\n");
        }

        _ = builder.Append('\n');
        AppendShadowedTypeAliases(builder);
        foreach (var baseRelation in record.Bases.Where(static relation =>
                     relation.IsPublic
                     && relation.PointerAdjustment is PointerAdjustmentKind.NativeAdjust))
        {
            AppendPointerAdjustment(builder, baseRelation);
        }

        foreach (var method in record.MethodModels)
        {
            AppendMethod(builder, method, method.NativeExportName);
        }

        return Task.FromResult(builder.ToString());
    }

    private void AppendShadowedTypeAliases(StringBuilder builder)
    {
        foreach (var typeName in record.MethodModels
                     .SelectMany(static method => method.Parameters
                         .Select(static parameter => parameter.Type)
                         .Prepend(method.ReturnType))
                     .Where(type => type.IsRecord && IsCppTypeShadowed(type.CppValueTypeName))
                     .Select(static type => type.CppValueTypeName)
                     .Distinct(StringComparer.Ordinal))
        {
            _ = builder.Append("using ").Append(GetNativeTypeAlias(typeName)).Append(" = ")
                .Append(typeName).Append(";\n");
        }

        if (record.MethodModels.Any(method => method.Parameters
            .Select(static parameter => parameter.Type)
            .Prepend(method.ReturnType)
            .Any(type => type.IsRecord && IsCppTypeShadowed(type.CppValueTypeName))))
        {
            _ = builder.Append('\n');
        }
    }

    private IEnumerable<string> GetRequiredHeaders(RecordModel recordModel)
    {
        var headers = new List<string>();
        var includedHeaders = new HashSet<string>(StringComparer.Ordinal);
        AddRecordHeaders(
            headers,
            includedHeaders,
            recordModel,
            new HashSet<RecordModel>(),
            recordModel.SourceHeader);
        AddHeader(headers, includedHeaders, recordModel.SourceHeader);
        var preambleHeaders = emissionProfile.NativePreambleHeaders.ToHashSet(StringComparer.Ordinal);
        return headers.Where(header => !preambleHeaders.Contains(header));
    }

    private static void AddRecordHeaders(
        List<string> headers,
        HashSet<string> includedHeaders,
        RecordModel recordModel,
        HashSet<RecordModel> visitedRecords,
        string deferredHeader)
    {
        if (!visitedRecords.Add(recordModel))
        {
            return;
        }

        foreach (var dependency in recordModel.Bases.Select(static relation => relation.Base)
                     .Concat(recordModel.NativeDependencyRecords)
                     .OrderBy(static dependency => dependency.SourceHeader, StringComparer.Ordinal))
        {
            AddRecordHeaders(headers, includedHeaders, dependency, visitedRecords, deferredHeader);
        }

        var localHeaders = new HashSet<string>(recordModel.NativeRequiredHeaders, StringComparer.Ordinal);
        AddHeaders(localHeaders, recordModel.Type);
        foreach (var field in recordModel.FieldModels)
        {
            AddHeaders(localHeaders, field.Type);
        }

        foreach (var method in recordModel.MethodModels)
        {
            AddHeaders(localHeaders, method.ReturnType);
            foreach (var parameter in method.Parameters)
            {
                AddHeaders(localHeaders, parameter.Type);
            }
        }

        foreach (var header in localHeaders
                     .Where(header => !string.Equals(header, recordModel.SourceHeader, StringComparison.Ordinal)
                                      && !string.Equals(header, deferredHeader, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            AddHeader(headers, includedHeaders, header);
        }

        if (string.Equals(recordModel.SourceHeader, deferredHeader, StringComparison.Ordinal))
        {
            return;
        }

        AddHeader(headers, includedHeaders, recordModel.SourceHeader);
    }

    private static void AddHeader(List<string> headers, HashSet<string> includedHeaders, string header)
    {
        if (!includedHeaders.Add(header))
        {
            return;
        }

        headers.Add(header);
    }

    private static void AddHeaders(HashSet<string> headers, TypeModel type)
    {
        foreach (var header in type.RequiredHeaders)
        {
            _ = headers.Add(header);
        }
    }

    private void AppendMethod(StringBuilder builder, MethodModel method, string exportName)
    {
        switch (method.Type)
        {
            case MethodModelType.NEW:
                AppendConstructor(builder, method, exportName);
                return;

            case MethodModelType.DELETE:
                _ = builder.Append("extern \"C\" void ").Append(exportName).Append('(')
                    .Append(record.Type.CppTypeName).Append("* self) noexcept\n{\n");
                if (record.ObjectKind is NativeObjectKind.IntrusiveHandle)
                {
                    AppendIntrusiveRelease(builder);
                }
                else
                {
                    _ = builder.Append("    if (self != nullptr)\n    {\n        using Native = ")
                        .Append(record.Type.CppTypeName)
                        .Append(";\n        self->~Native();\n    }\n");
                }

                _ = builder.Append("}\n\n");
                return;

            case MethodModelType.VALUE_DELETE:
                _ = builder.Append("extern \"C\" void ").Append(exportName).Append('(')
                    .Append(record.Type.CppTypeName).Append("* self) noexcept\n{\n")
                    .Append("    if (self != nullptr)\n    {\n        using Native = ")
                    .Append(record.Type.CppTypeName)
                    .Append(";\n        self->~Native();\n    }\n}\n\n");
                return;

            case MethodModelType.INTRUSIVE_RELEASE:
                _ = builder.Append("extern \"C\" void ").Append(exportName).Append('(')
                    .Append(record.Type.CppTypeName).Append("* self) noexcept\n{\n");
                AppendIntrusiveRelease(builder);
                _ = builder.Append("}\n\n");
                return;

            case MethodModelType.NORMAL:
            case MethodModelType.OPERATOR:
            case MethodModelType.IMPLICIT:
            case MethodModelType.EXPLICIT:
                AppendInvocation(builder, method, exportName);
                return;

            default:
                throw new InvalidOperationException($"Unsupported method model type '{method.Type}'.");
        }
    }

    private void AppendIntrusiveRelease(StringBuilder builder)
    {
        _ = builder.Append("    if (self != nullptr && ").Append(emissionProfile.IntrusiveReleaseCondition)
            .Append(")\n    {\n        ").Append(emissionProfile.IntrusiveDeleteStatement).Append("\n    }\n");
    }

    private void AppendConstructor(StringBuilder builder, MethodModel method, string exportName)
    {
        _ = builder.Append("extern \"C\" ");
        if (record.ObjectKind is NativeObjectKind.IntrusiveHandle)
        {
            _ = builder.Append(record.Type.CppTypeName).Append("* ").Append(exportName).Append('(');
            AppendParameters(builder, method.Parameters, includeReceiver: false, method.IsConst);
            AppendErrorParameter(builder, method, method.Parameters.Count > 0);
            _ = builder.Append(") noexcept")
                .Append("\n{\n");
            AppendTryStart(builder, method);
            _ = builder.Append("    auto* result = new ");
            var allocator = record.UsesAllocatorPlacementNew
                ? method.Parameters.FirstOrDefault(static parameter => parameter.IsPlacementAllocator)
                : null;
            if (allocator is not null)
            {
                _ = builder.Append("(*").Append(allocator.Name).Append(") ");
            }

            _ = builder.Append(record.Type.CppTypeName).Append('(');
            AppendArguments(builder, method);
            _ = builder.Append(");\n    ").Append(emissionProfile.IntrusiveRetainStatement)
                .Append("\n    return result;\n");
            AppendTryEnd(builder, method, "return nullptr;");
            _ = builder.Append("}\n\n");
            return;
        }

        _ = builder.Append("void ").Append(exportName).Append('(').Append(record.Type.CppTypeName)
            .Append("* result");
        foreach (var parameter in method.Parameters)
        {
            _ = builder.Append(", ");
            AppendTransportParameter(builder, parameter);
        }

        AppendErrorParameter(builder, method, hasPrevious: true);
        _ = builder.Append(") noexcept\n{\n");
        AppendTryStart(builder, method);
        _ = builder.Append("    ::new (result) ").Append(record.Type.CppTypeName).Append('(');
        AppendArguments(builder, method);
        _ = builder.Append(");\n");
        AppendTryEnd(builder, method, "return;");
        _ = builder.Append("}\n\n");
    }

    private void AppendInvocation(StringBuilder builder, MethodModel method, string exportName)
    {
        var returnsHandleValue = method.ReturnType.IsIntrusiveHandle
                                 && method.ReturnType.Transport.Indirections.Count is 0;
        var returnsRecordValue = !returnsHandleValue
                                 && method.ReturnType.IsRecord
                                 && method.ReturnType.Transport.Indirections.Count is 0;
        var returnsReference = GetFirstIndirection(method.ReturnType)
                               is TypeIndirectionKind.LValueReference or TypeIndirectionKind.RValueReference;
        _ = builder.Append("extern \"C\" ");
        if (returnsHandleValue)
        {
            _ = builder.Append(method.ReturnType.IntrusiveHandleElementCppType).Append("* ")
                .Append(exportName).Append('(');
        }
        else if (returnsRecordValue)
        {
            _ = builder.Append("void ").Append(exportName).Append('(');
        }
        else if (returnsReference)
        {
            _ = builder.Append(GetReferencedCppType(method.ReturnType)).Append("* ")
                .Append(exportName).Append('(');
        }
        else
        {
            _ = builder.Append(method.ReturnType.CppTypeName).Append(' ').Append(exportName).Append('(');
        }

        AppendParameters(builder, method.Parameters, !method.IsStatic, method.IsConst);
        if (returnsRecordValue)
        {
            if (!method.IsStatic || method.Parameters.Count > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(method.ReturnType.CppValueTypeName).Append("* result");
        }

        AppendErrorParameter(
            builder,
            method,
            !method.IsStatic || method.Parameters.Count > 0 || returnsRecordValue);
        _ = builder.Append(") noexcept\n{\n");
        AppendTryStart(builder, method);
        _ = builder.Append("    ");
        if (returnsHandleValue)
        {
            _ = builder.Append("auto resultHandle = ");
        }
        else if (returnsRecordValue)
        {
            _ = builder.Append("::new (result) ").Append(method.ReturnType.CppValueTypeName).Append('(');
        }
        else if (returnsReference)
        {
            _ = builder.Append("return &(");
        }
        else if (!method.IsReturnVoid)
        {
            _ = builder.Append("return ");
        }

        if (method.Type is MethodModelType.IMPLICIT or MethodModelType.EXPLICIT)
        {
            _ = builder.Append("static_cast<").Append(method.ReturnType.CppTypeName).Append(">(*self)");
        }
        else if (method.Type is MethodModelType.OPERATOR)
        {
            _ = builder.Append("self->operator").Append(method.MethodName);
            AppendCallArguments(builder, method.Parameters);
        }
        else if (method.IsStatic)
        {
            _ = builder.Append(record.Type.CppTypeName).Append("::")
                .Append(GetNativeMethodName(method));
            AppendCallArguments(builder, method.Parameters);
        }
        else
        {
            AppendMemberInvocation(builder, method);
        }

        if (returnsHandleValue)
        {
            _ = builder.Append(";\n    auto* result = resultHandle.get();\n")
                .Append("    if (result != nullptr)\n    {\n        ").Append(emissionProfile.IntrusiveRetainStatement)
                .Append("\n    }\n")
                .Append("    return result;\n");
        }
        else if (returnsRecordValue)
        {
            _ = builder.Append(')');
        }
        else if (returnsReference)
        {
            _ = builder.Append(')');
        }

        if (!returnsHandleValue)
        {
            _ = builder.Append(";\n");
        }

        AppendTryEnd(builder, method, GetFailureReturn(
            method,
            returnsRecordValue,
            returnsReference || returnsHandleValue));
        _ = builder.Append("}\n\n");
    }

    private void AppendMemberInvocation(StringBuilder builder, MethodModel method)
    {
        _ = builder.Append("(self->*static_cast<").Append(GetMemberPointerCppType(method.ReturnType)).Append(" (")
            .Append(record.Type.CppTypeName).Append("::*)(")
            .AppendJoin(", ", method.Parameters.Select(parameter => GetMemberPointerCppType(parameter.Type)))
            .Append(')');
        if (method.IsConst)
        {
            _ = builder.Append(" const");
        }

        if (method.IsVolatile)
        {
            _ = builder.Append(" volatile");
        }

        _ = builder.Append(method.RefQualifier);

        if (method.NoExceptions)
        {
            _ = builder.Append(" noexcept");
        }

        _ = builder.Append(">(&").Append(record.Type.CppTypeName).Append("::")
            .Append(GetNativeMethodName(method)).Append("))");
        AppendCallArguments(builder, method.Parameters);
    }

    private string GetMemberPointerCppType(TypeModel type)
    {
        var value = type.CppTypeName;
        var recordName = type.CppValueTypeName;
        if (!type.IsRecord)
        {
            return value;
        }

        var index = value.IndexOf(recordName, StringComparison.Ordinal);
        if (index < 0)
        {
            return value;
        }

        var replacement = IsCppTypeShadowed(recordName) ? GetNativeTypeAlias(recordName) : "::" + recordName;
        return string.Concat(value.AsSpan(0, index), replacement, value.AsSpan(index + recordName.Length));
    }

    private bool IsCppTypeShadowed(string typeName)
    {
        return record.MethodModels.Any(method => string.Equals(
            method.NativeExportName,
            typeName,
            StringComparison.Ordinal));
    }

    private static string GetNativeTypeAlias(string typeName)
    {
        return typeName + "_NativeType";
    }

    private static string GetNativeMethodName(MethodModel method)
    {
        return string.IsNullOrEmpty(method.NativeMethodName) ? method.MethodName : method.NativeMethodName;
    }

    private static string GetReferencedCppType(TypeModel type)
    {
        var value = type.CppTypeName.TrimEnd();
        if (value.EndsWith("&&", StringComparison.Ordinal))
        {
            return value[..^2].TrimEnd();
        }

        return value.EndsWith('&') ? value[..^1].TrimEnd() : value;
    }

    private void AppendParameters(
        StringBuilder builder,
        IReadOnlyList<ParameterModel> parameters,
        bool includeReceiver,
        bool receiverIsConst)
    {
        var started = false;
        if (includeReceiver)
        {
            if (receiverIsConst)
            {
                _ = builder.Append("const ");
            }

            _ = builder.Append(record.Type.CppTypeName).Append("* self");
            started = true;
        }

        foreach (var parameter in parameters)
        {
            if (started)
            {
                _ = builder.Append(", ");
            }

            AppendTransportParameter(builder, parameter);
            started = true;
        }
    }

    private static void AppendCallArguments(StringBuilder builder, IReadOnlyList<ParameterModel> parameters)
    {
        _ = builder.Append('(');
        AppendArguments(builder, parameters);
        _ = builder.Append(')');
    }

    private static void AppendArguments(StringBuilder builder, IReadOnlyList<ParameterModel> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(", ");
            }

            AppendTransportArgument(builder, parameters[index]);
        }
    }

    private static void AppendArguments(StringBuilder builder, MethodModel method)
    {
        AppendArguments(builder, method.Parameters);
        for (var index = 0; index < method.NativeDefaultArguments.Count; index++)
        {
            if (method.Parameters.Count > 0 || index > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(method.NativeDefaultArguments[index]);
        }
    }

    private static void AppendTransportParameter(StringBuilder builder, ParameterModel parameter)
    {
        var type = parameter.Type;
        var firstIndirection = GetFirstIndirection(type);
        if (firstIndirection is TypeIndirectionKind.LValueReference or TypeIndirectionKind.RValueReference)
        {
            _ = builder.Append(GetReferencedCppType(type)).Append("* ").Append(parameter.Name);
            return;
        }

        if (type.IsRecord && type.Transport.Indirections.Count is 0)
        {
            _ = builder.Append("const ").Append(type.CppValueTypeName).Append("* ").Append(parameter.Name);
            return;
        }

        _ = builder.Append(type.CppTypeName).Append(' ').Append(parameter.Name);
    }

    private static void AppendTransportArgument(StringBuilder builder, ParameterModel parameter)
    {
        var firstIndirection = GetFirstIndirection(parameter.Type);
        if (firstIndirection is TypeIndirectionKind.RValueReference)
        {
            _ = builder.Append("std::move(*").Append(parameter.Name).Append(')');
        }
        else if (firstIndirection is TypeIndirectionKind.LValueReference
                 || (parameter.Type.IsRecord && parameter.Type.Transport.Indirections.Count is 0))
        {
            _ = builder.Append('*').Append(parameter.Name);
        }
        else
        {
            _ = builder.Append(parameter.Name);
        }
    }

    private static TypeIndirectionKind? GetFirstIndirection(TypeModel type)
    {
        return type.Transport.Indirections.Count is 0
            ? null
            : type.Transport.Indirections[0].Kind;
    }

    private void AppendErrorParameter(StringBuilder builder, MethodModel method, bool hasPrevious)
    {
        if (method.NoExceptions)
        {
            return;
        }

        if (hasPrevious)
        {
            _ = builder.Append(", ");
        }

        _ = builder.Append(emissionProfile.NativeErrorType).Append("* __error");
    }

    private static void AppendTryStart(StringBuilder builder, MethodModel method)
    {
        if (method.NoExceptions)
        {
            return;
        }

        _ = builder.Append("    if (__error != nullptr)\n    {\n        *__error = {};\n    }\n\n    try\n    {\n");
    }

    private void AppendTryEnd(StringBuilder builder, MethodModel method, string failureReturn)
    {
        if (method.NoExceptions)
        {
            return;
        }

        _ = builder.Append("    }\n");
        foreach (var projection in emissionProfile.NativeExceptionProjections)
        {
            _ = builder.Append("    catch (const ").Append(projection.CppType).Append("& exception)\n    {\n")
                .Append("        ").Append(emissionProfile.NativeErrorSetter).Append("(__error, ")
                .Append(projection.Code).Append(", ").Append(projection.NativeTypeExpression).Append(", ")
                .Append(projection.MessageExpression).Append(");\n        ").Append(failureReturn).Append("\n    }\n");
        }

        _ = builder.Append("    catch (...)\n    {\n        ").Append(emissionProfile.NativeErrorSetter)
            .Append("(__error, ").Append(emissionProfile.UnknownNativeExceptionCode)
            .Append(", nullptr, nullptr);\n        ").Append(failureReturn).Append("\n    }\n");
    }

    private static string GetFailureReturn(
        MethodModel method,
        bool returnsRecordValue,
        bool returnsReference)
    {
        if (method.IsReturnVoid || returnsRecordValue)
        {
            return "return;";
        }

        if (returnsReference || method.ReturnType.IsRecord)
        {
            return "return nullptr;";
        }

        return "return {};";
    }

    private void AppendPointerAdjustment(StringBuilder builder, BaseRelationModel relation)
    {
        _ = builder.Append("extern \"C\" ").Append(relation.Base.Type.CppTypeName).Append("* ")
            .Append(NativeExportNameBuilder.GetPointerAdjustmentName(record, relation.Base))
            .Append('(').Append(record.Type.CppTypeName).Append("* self) noexcept\n{\n    return static_cast<")
            .Append(relation.Base.Type.CppTypeName).Append("*>(self);\n}\n\n");
    }
}