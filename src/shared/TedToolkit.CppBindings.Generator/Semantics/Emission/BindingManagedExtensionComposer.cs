// -----------------------------------------------------------------------
// <copyright file="BindingManagedExtensionComposer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.CppBindings.Generator.Semantics.BindingManagedExtensionComposer>;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Composes generated managed extension members for one normalized C++ record.
/// </summary>
/// <param name="record">The record whose operations are projected.</param>
/// <param name="profile">The provider emission profile.</param>
/// <param name="recordCatalog">The completed record catalog.</param>
/// <param name="nativeFunctionIndices">Native function-table slots.</param>
internal sealed class BindingManagedExtensionComposer(
    RecordModel record,
    BindingEmissionProfile profile,
    IReadOnlyDictionary<string, RecordModel>? recordCatalog,
    IReadOnlyDictionary<string, int>? nativeFunctionIndices)
{
    private bool? _usesGenericReceiver;

    private bool UsesGenericReceiver
    {
        get
        {
            return _usesGenericReceiver ??= recordCatalog?.Values.Any(candidate =>
                !ReferenceEquals(candidate, record)
                && TryGetInheritancePath(candidate, record, out _)) is true;
        }
    }

    /// <summary>
    /// Adds the applicable extension container to the generated namespace.
    /// </summary>
    /// <param name="nameSpace">The generated namespace.</param>
    internal void AddTo(NameSpace nameSpace)
    {
        ArgumentNullException.ThrowIfNull(nameSpace);
        var methods = record.MethodModels.Where(method =>
                (method.Type is MethodModelType.NEW
                 && !record.IsAbstract
                 && (record.ObjectKind is NativeObjectKind.Value
                     || record.MethodModels.Any(static candidate => candidate.Type is MethodModelType.DELETE)))
                || (method.Type is MethodModelType.NORMAL
                    && (GetFirstIndirection(method.ReturnType) is TypeIndirectionKind.LValueReference
                        || (method.ReturnType.IsIntrusiveHandle
                            && method.ReturnType.Transport.Indirections.Count is 0)
                        || (method.ReturnType.IsRecord
                            && method.ReturnType.Transport.Indirections.Count is 0
                            && !method.ReturnType.IsIntrusiveHandle)
                        || (!method.ReturnType.IsRecord
                            && method.ReturnType.Transport.Indirections.Count is 0))))
            .ToArray();
        if (methods.Length is 0)
        {
            return;
        }

        var recordName = record.Type.CSharpTypeName;
        var extensionName = record.TemplateProjection?.FixedTypeName ?? recordName;
        var declaration = Class(extensionName + "Extensions").Static.Unsafe;
        declaration = profile.IsInternal ? declaration.Internal : declaration.Public;
        foreach (var method in methods)
        {
            if (method.Type is MethodModelType.NEW)
            {
                declaration.AddMember(ComposeConstructor(method, recordName));
                continue;
            }

            declaration.AddMember(ComposeExtension(method, recordName, borrowedHandleReceiver: false));
            if (!method.IsStatic && record.ObjectKind is NativeObjectKind.IntrusiveHandle)
            {
                declaration.AddMember(ComposeExtension(method, recordName, borrowedHandleReceiver: true));
            }
        }

        if (UsesGenericReceiver)
        {
            declaration.AddMember(ComposePointerAdjustment(recordName));
        }

        nameSpace.AddMember(declaration);
    }

    private Method ComposeConstructor(MethodModel method, string recordName)
    {
        var shape = new CallShape();
        foreach (var parameter in method.Parameters)
        {
            AddParameter(parameter, shape);
        }

        if (record.ObjectKind is not NativeObjectKind.IntrusiveHandle)
        {
            shape.NativeParameterTypes.Insert(0, recordName + "*");
            shape.NativeArguments.Insert(0, "__resultPointer");
        }

        AddErrorTransport(method, shape);
        var returnType = record.ObjectKind switch
        {
            NativeObjectKind.IntrusiveHandle => $"{profile.ManagedOwningIntrusiveHandle}<{recordName}>",
            NativeObjectKind.Owned => $"global::TedToolkit.CppBindings.Owned<{recordName}>",
            _ => recordName,
        };
        var declaration = Method("Create", ReturnType(new DataType(returnType))).Public.Static;
        AddPublicParameters(declaration, shape);
        declaration.AddStatement(new Custom(ComposeConstructorBody(method, recordName, shape)));
        return declaration;
    }

    private string ComposeConstructorBody(MethodModel method, string recordName, CallShape shape)
    {
        var prefix = ErrorDeclaration(method);
        if (record.ObjectKind is NativeObjectKind.IntrusiveHandle)
        {
            shape.NativeParameterTypes.Add(recordName + "*");
            var invocation = "var __result = " + Invoke(method.NativeExportName, shape.NativeParameterTypes, shape.NativeArguments);
            var statements = new List<string>() { invocation, };
            statements.AddRange(KeepAliveStatements(shape.ParameterOwners));
            AddErrorCheck(method, statements);
            statements.Add(
                $"return new {profile.ManagedOwningIntrusiveHandle}<{recordName}>(__result, "
                + $"(delegate* unmanaged[Cdecl]<{recordName}*, void>){GetNativeFunctionExpression(GetDeleteExportName())});");
            return prefix + ComposeFixedBlock(shape.Pins, statements);
        }

        if (record.ObjectKind is NativeObjectKind.Owned)
        {
            var result = "var __result = new global::TedToolkit.CppBindings.Owned<" + recordName + ">("
                         + $"(delegate* unmanaged[Cdecl]<{recordName}*, void>)"
                         + GetNativeFunctionExpression(GetDeleteExportName()) + ");";
            var invocation = InvokeVoid(method.NativeExportName, shape.NativeParameterTypes, shape.NativeArguments);
            var fixedStatements = new List<string>() { invocation, };
            fixedStatements.AddRange(KeepAliveStatements(shape.ParameterOwners));
            AddErrorCheck(method, fixedStatements);
            var fixedBlock = ComposeFixedBlock(
                PrependPin((recordName, "__resultPointer", "&__result.Value"), shape.Pins),
                fixedStatements);
            return prefix + JoinLines(
            [
                result,
                "var constructed = false;",
                "try",
                "{",
                Indent(fixedBlock, 4),
                "    constructed = true;",
                "    return __result;",
                "}",
                "finally",
                "{",
                "    if (!constructed)",
                "    {",
                "        GC.SuppressFinalize(__result);",
                "    }",
                "}",
            ]);
        }

        var valueStatements = new List<string>()
        {
            $"{recordName} __result = default;",
            $"{recordName}* __resultPointer = &__result;",
        };
        var valueInvocation = InvokeVoid(method.NativeExportName, shape.NativeParameterTypes, shape.NativeArguments);
        var valueCall = new List<string>() { valueInvocation, };
        valueCall.AddRange(KeepAliveStatements(shape.ParameterOwners));
        AddErrorCheck(method, valueCall);
        valueStatements.Add(ComposeFixedBlock(shape.Pins, valueCall));
        valueStatements.Add("return __result;");
        return prefix + JoinLines(valueStatements);
    }

    private Method ComposeExtension(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        if (GetFirstIndirection(method.ReturnType) is TypeIndirectionKind.LValueReference)
        {
            return ComposeReferenceReturn(method, recordName, borrowedHandleReceiver);
        }

        if (method.ReturnType.IsIntrusiveHandle)
        {
            return ComposeHandleReturn(method, recordName, borrowedHandleReceiver);
        }

        return method.ReturnType.IsRecord
            ? ComposeRecordReturn(method, recordName, borrowedHandleReceiver)
            : ComposeScalarReturn(method, recordName, borrowedHandleReceiver);
    }

    private Method ComposeRecordReturn(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        if (recordCatalog is null
            || !recordCatalog.TryGetValue(method.ReturnType.CppValueTypeName, out var resultRecord))
        {
            throw new NotSupportedException(
                $"Record result '{method.ReturnType.CppValueTypeName}' has no completed model.");
        }

        var shape = CreateCallShape(method, recordName, borrowedHandleReceiver);
        var resultName = resultRecord.Type.CSharpTypeName;
        shape.NativeParameterTypes.Add(resultName + "*");
        shape.NativeArguments.Add("__resultPointer");
        AddErrorTransport(method, shape);
        var returnsOwnedValue = resultRecord.ObjectKind is NativeObjectKind.Owned or NativeObjectKind.IntrusiveHandle;
        var returnType = returnsOwnedValue
            ? $"global::TedToolkit.CppBindings.Owned<{resultName}>"
            : resultName;
        var declaration = CreateMethod(method, new DataType(returnType), shape);
        var callStatements = CreateRecordCallStatements(method, shape, borrowedHandleReceiver);
        if (returnsOwnedValue)
        {
            var destructorType = resultRecord.ObjectKind is NativeObjectKind.IntrusiveHandle
                ? MethodModelType.VALUE_DELETE
                : MethodModelType.DELETE;
            var destructor = resultRecord.MethodModels.FirstOrDefault(value => value.Type == destructorType)
                             ?? throw new NotSupportedException(
                                 $"Method '{record.Type.CppTypeName}::{method.MethodName}' returns nontrivial "
                                 + $"'{resultRecord.Type.CppTypeName}' by value, but that type has no callable destructor.");
            var fixedBlock = ComposeFixedBlock(
                PrependPin((resultName, "__resultPointer", "&__result.Value"), shape.Pins),
                callStatements);
            declaration.AddStatement(new Custom(ErrorDeclaration(method) + JoinLines(
            [
                $"var __result = new global::TedToolkit.CppBindings.Owned<{resultName}>("
                + $"(delegate* unmanaged[Cdecl]<{resultName}*, void>){GetNativeFunctionExpression(destructor.NativeExportName)});",
                "var constructed = false;",
                "try",
                "{",
                Indent(fixedBlock, 4),
                "    constructed = true;",
                "    return __result;",
                "}",
                "finally",
                "{",
                "    if (!constructed)",
                "    {",
                "        GC.SuppressFinalize(__result);",
                "    }",
                "}",
            ])));
            return declaration;
        }

        declaration.AddStatement(new Custom(ErrorDeclaration(method) + JoinLines(
        [
            $"{resultName} __result = default;",
            $"{resultName}* __resultPointer = &__result;",
            ComposeFixedBlock(shape.Pins, callStatements),
            "return __result;",
        ])));
        return declaration;
    }

    private List<string> CreateRecordCallStatements(
        MethodModel method,
        CallShape shape,
        bool borrowedHandleReceiver)
    {
        var invocation = InvokeVoid(method.NativeExportName, shape.NativeParameterTypes, shape.NativeArguments);
        var statements = new List<string>() { invocation, };
        AddReceiverKeepAlive(method, borrowedHandleReceiver, statements);
        statements.AddRange(KeepAliveStatements(shape.ParameterOwners));
        AddErrorCheck(method, statements);
        return statements;
    }

    private Method ComposeHandleReturn(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        var shape = CreateCallShape(method, recordName, borrowedHandleReceiver);
        AddErrorTransport(method, shape);
        var elementType = method.ReturnType.IntrusiveHandleElementType;
        var declaration = CreateMethod(
            method,
            new DataType($"{profile.ManagedOwningIntrusiveHandle}<{elementType}>?"),
            shape);
        shape.NativeParameterTypes.Add(elementType + "*");
        var invocation = "var __result = "
                         + Invoke(method.NativeExportName, shape.NativeParameterTypes, shape.NativeArguments);
        var statements = new List<string>() { invocation, };
        AddReceiverKeepAlive(method, borrowedHandleReceiver, statements);
        statements.AddRange(KeepAliveStatements(shape.ParameterOwners));
        AddErrorCheck(method, statements);
        statements.Add("if (__result == null)\n{\n    return null;\n}");
        statements.Add(
            $"return new {profile.ManagedOwningIntrusiveHandle}<{elementType}>(__result, "
            + $"(delegate* unmanaged[Cdecl]<{elementType}*, void>)"
            + GetNativeFunctionExpression(NativeExportNameBuilder.GetIntrusiveReleaseName(
                method.ReturnType.IntrusiveHandleElementCppType)) + ");");
        declaration.AddStatement(new Custom(ErrorDeclaration(method) + ComposeFixedBlock(shape.Pins, statements)));
        return declaration;
    }

    private Method ComposeScalarReturn(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        var shape = CreateCallShape(method, recordName, borrowedHandleReceiver);
        AddErrorTransport(method, shape);
        var returnType = method.ReturnType.CSharpPublicType.ToCode();
        var declaration = CreateMethod(method, method.ReturnType.CSharpPublicType, shape);
        var invocation = Invoke(
            method.NativeExportName,
            shape.NativeParameterTypes.Append(method.ReturnType.CSharpPInvokeType.ToCode()),
            shape.NativeArguments);
        var call = method.IsReturnVoid ? invocation : "var __result = " + invocation;
        var statements = new List<string>() { call, };
        AddReceiverKeepAlive(method, borrowedHandleReceiver, statements);
        statements.AddRange(KeepAliveStatements(shape.ParameterOwners));
        AddErrorCheck(method, statements);
        if (!method.IsReturnVoid)
        {
            var pinvokeReturnType = method.ReturnType.CSharpPInvokeType.ToCode();
            var cast = string.Equals(returnType, pinvokeReturnType, StringComparison.Ordinal)
                ? ""
                : $"({returnType})";
            statements.Add($"return {cast}__result;");
        }

        declaration.AddStatement(new Custom(ErrorDeclaration(method) + ComposeFixedBlock(shape.Pins, statements)));
        return declaration;
    }

    private Method ComposeReferenceReturn(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        var returnType = GetCSharpValueType(method.ReturnType);
        var returnTypeName = returnType.ToCode();
        var shape = CreateCallShape(method, recordName, borrowedHandleReceiver);
        AddErrorTransport(method, shape);
        var declaration = CreateMethod(
            method,
            IsReferencedValueConst(method.ReturnType) ? returnType.RefReadonly : returnType.Ref,
            shape);
        declaration.AddRootDescription(new DescriptionRemarks([
            new DescriptionText("The returned reference is borrowed from native storage. "
                                + "The caller must preserve the native owner and obey the original C++ invalidation rules."),
        ]));
        var invocation = "var __result = " + Invoke(
            method.NativeExportName,
            shape.NativeParameterTypes.Append(returnTypeName + "*"),
            shape.NativeArguments);
        var statements = new List<string>() { invocation, };
        AddReceiverKeepAlive(method, borrowedHandleReceiver, statements);
        statements.AddRange(KeepAliveStatements(shape.ParameterOwners));
        AddErrorCheck(method, statements);
        statements.Add("return ref *__result;");
        declaration.AddStatement(new Custom(ErrorDeclaration(method) + ComposeFixedBlock(shape.Pins, statements)));
        return declaration;
    }

    private Method CreateMethod(MethodModel method, DataType returnType, CallShape shape)
    {
        var declaration = Method(
            GetPublicMethodName(method).ToValidIdentifier(),
            ReturnType(returnType)).Public.Static;
        AddPublicParameters(declaration, shape);
        AddReceiverTypeParameter(method, declaration);
        return declaration;
    }

    private CallShape CreateCallShape(MethodModel method, string recordName, bool borrowedHandleReceiver)
    {
        var shape = new CallShape();
        if (!method.IsStatic)
        {
            AddReceiver(recordName, method, borrowedHandleReceiver, shape);
        }

        foreach (var parameter in method.Parameters)
        {
            AddParameter(parameter, shape);
        }

        return shape;
    }

    private void AddReceiver(
        string recordName,
        MethodModel method,
        bool borrowedHandleReceiver,
        CallShape shape)
    {
        var receiverType = UsesGenericReceiver ? "TReceiver" : recordName;
        Parameter receiver;
        if (borrowedHandleReceiver)
        {
            receiver = new Parameter(
                new DataType($"{profile.ManagedBorrowedIntrusiveHandle}<{receiverType}>").In,
                "self").This;
        }
        else
        {
            receiver = record.ObjectKind switch
            {
                NativeObjectKind.IntrusiveHandle => new Parameter(
                    new DataType($"{profile.ManagedOwningIntrusiveHandle}<{receiverType}>"), "self").This,
                NativeObjectKind.Owned => new Parameter(
                    new DataType($"global::TedToolkit.CppBindings.Owned<{receiverType}>"), "self").This,
                _ when UsesGenericReceiver => new Parameter(new DataType(receiverType).Ref, "self").This,
                _ when method.IsConst => new Parameter(new DataType(receiverType).In, "self").This,
                _ => new Parameter(new DataType(receiverType).Ref, "self").This,
            };
        }

        shape.PublicParameters.Add(receiver);
        shape.NativeParameterTypes.Add(recordName + "*");
        shape.NativeArguments.Add(UsesGenericReceiver ? "AdjustReceiver(selfPointer)" : "selfPointer");
        shape.Pins.Add((receiverType, "selfPointer",
            borrowedHandleReceiver || record.ObjectKind is NativeObjectKind.IntrusiveHandle or NativeObjectKind.Owned
                ? "&self.Value"
                : "&self"));
    }

    private void AddParameter(ParameterModel parameter, CallShape shape)
    {
        var parameterName = parameter.Name.ToValidIdentifier();
        var firstIndirection = GetFirstIndirection(parameter.Type);
        var isReference = firstIndirection is TypeIndirectionKind.LValueReference
            or TypeIndirectionKind.RValueReference;
        var isRecordValue = parameter.Type.IsRecord && parameter.Type.Transport.Indirections.Count is 0;
        if (!parameter.Type.IsIntrusiveHandle
            && parameter.Type.IsRecord
            && recordCatalog is not null
            && recordCatalog.TryGetValue(parameter.Type.CppValueTypeName, out var parameterRecord)
            && parameterRecord.ObjectKind is NativeObjectKind.Owned or NativeObjectKind.IntrusiveHandle)
        {
            var ownerTypeName = parameterRecord.Type.CSharpTypeName;
            var ownerName = parameterRecord.ObjectKind is NativeObjectKind.Owned
                ? "global::TedToolkit.CppBindings.Owned"
                : profile.ManagedOwningIntrusiveHandle;
            shape.PublicParameters.Add(new Parameter(new DataType($"{ownerName}<{ownerTypeName}>"), parameterName));
            shape.NativeParameterTypes.Add(ownerTypeName + "*");
            var pointerName = parameterName + "Pointer";
            shape.NativeArguments.Add(pointerName);
            shape.Pins.Add((ownerTypeName, pointerName, "&" + parameterName + ".Value"));
            shape.ParameterOwners.Add(parameterName);
            return;
        }

        if (isReference || isRecordValue)
        {
            var referencedType = GetCSharpValueType(parameter.Type);
            var referencedTypeName = referencedType.ToCode();
            var referencedDataType = IsReferencedValueConst(parameter.Type) || isRecordValue
                ? referencedType.In
                : referencedType.Ref;
            var publicParameter = new Parameter(referencedDataType, parameterName);
            shape.PublicParameters.Add(publicParameter);
            var pinvokeType = parameter.Type.CSharpPInvokeType.ToCode();
            shape.NativeParameterTypes.Add(pinvokeType.EndsWith('*') ? pinvokeType : referencedTypeName + "*");
            var pointerName = parameterName + "Pointer";
            shape.NativeArguments.Add(pointerName);
            shape.Pins.Add((referencedTypeName, pointerName, "&" + parameterName));
            return;
        }

        var publicDataType = parameter.Type.CSharpPublicType;
        var publicType = publicDataType.ToCode();
        var nativeType = parameter.Type.CSharpPInvokeType.ToCode();
        if (firstIndirection is TypeIndirectionKind.PointerIndirection
            && publicDataType.StorageKind is not StorageKind.NONE)
        {
            var valueTypeName = GetCSharpValueType(parameter.Type).ToCode();
            var publicParameter = new Parameter(publicDataType, parameterName);
            shape.PublicParameters.Add(publicParameter);
            shape.NativeParameterTypes.Add(nativeType);
            var pointerName = parameterName + "Pointer";
            shape.NativeArguments.Add(pointerName);
            shape.Pins.Add((valueTypeName, pointerName, "&" + parameterName));
            return;
        }

        if (firstIndirection is TypeIndirectionKind.PointerIndirection
            && publicType is "global::System.ReadOnlySpan<byte>")
        {
            shape.PublicParameters.Add(new Parameter(publicDataType, parameterName));
            shape.NativeParameterTypes.Add(nativeType);
            var pointerName = parameterName + "Pointer";
            shape.NativeArguments.Add(pointerName);
            shape.Pins.Add(("byte", pointerName, parameterName));
            return;
        }

        shape.PublicParameters.Add(new Parameter(publicDataType, parameterName));
        shape.NativeParameterTypes.Add(nativeType);
        shape.NativeArguments.Add(string.Equals(publicType, nativeType, StringComparison.Ordinal)
            ? parameterName
            : $"({nativeType}){parameterName}");
    }

    private void AddReceiverTypeParameter(MethodModel method, Method declaration)
    {
        if (method.IsStatic || !UsesGenericReceiver)
        {
            return;
        }

        var parameter = new TypeParameter("TReceiver")
            .AddUnmanagedConstraint()
            .AddConstraint(new DataType(GetManagedInterfaceName(record)));
        if (record.ObjectKind is NativeObjectKind.IntrusiveHandle
            && record.Type.CSharpInterfaceName != profile.IntrusiveRootInterfaceName)
        {
            parameter.AddConstraint(new DataType(profile.ManagedIntrusiveRootInterface));
        }
        else if (record.ObjectKind is NativeObjectKind.Owned)
        {
            parameter.AddConstraint(new DataType("global::TedToolkit.CppBindings.ICppRaii"));
        }

        declaration.AddTypeParameter(parameter);
    }

    private Method ComposePointerAdjustment(string recordName)
    {
        var declaration = Method(
            "AdjustReceiver",
            ReturnType(new DataType(recordName + "*"))).Private.Static;
        declaration.AddTypeParameter(new TypeParameter("TReceiver")
            .AddUnmanagedConstraint()
            .AddConstraint(new DataType(GetManagedInterfaceName(record))));
        declaration.AddParameter(new Parameter(new DataType("TReceiver*"), "pointer"));
        var directReturn = $"if (typeof(TReceiver) == typeof({recordName}))\n"
                           + $"{{\n    return ({recordName}*)pointer;\n}}";
        var statements = new List<string>() { directReturn, };
        foreach (var derived in GetDerivedRecords())
        {
            if (!TryGetInheritancePath(derived, record, out var path))
            {
                continue;
            }

            var pointerExpression = $"({derived.Type.CSharpTypeName}*)pointer";
            foreach (var step in path)
            {
                if (step.Relation.PointerAdjustment is PointerAdjustmentKind.Identity)
                {
                    continue;
                }

                pointerExpression = "((delegate* unmanaged[Cdecl]<void*, void*>)"
                                    + GetNativeFunctionExpression(NativeExportNameBuilder.GetPointerAdjustmentName(
                                        step.Derived, step.Relation.Base))
                                    + $")({pointerExpression})";
            }

            pointerExpression = $"({recordName}*){pointerExpression}";

            statements.Add(
                $"if (typeof(TReceiver) == typeof({derived.Type.CSharpTypeName}))\n"
                + $"{{\n    return {pointerExpression};\n}}");
        }

        statements.Add(
            "throw new global::System.NotSupportedException("
            + $"$\"No native pointer adjustment is generated from {{typeof(TReceiver)}} to {recordName}.\");");
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private string GetPublicMethodName(MethodModel method)
    {
        if (!method.IsStatic || method.Parameters.Count is 0)
        {
            return method.MethodName;
        }

        var conflictsWithInstance = record.MethodModels.Any(candidate =>
            !candidate.IsStatic
            && string.Equals(candidate.MethodName, method.MethodName, StringComparison.Ordinal)
            && candidate.Parameters.Count + 1 == method.Parameters.Count
            && string.Equals(
                method.Parameters[0].Type.CppValueTypeName,
                record.Type.CppTypeName,
                StringComparison.Ordinal)
            && candidate.Parameters.Select(static parameter => parameter.Type.CSharpPInvokeType.ToCode())
                .SequenceEqual(method.Parameters.Skip(1)
                    .Select(static parameter => parameter.Type.CSharpPInvokeType.ToCode()),
                    StringComparer.Ordinal));
        return conflictsWithInstance ? method.MethodName + "_1" : method.MethodName;
    }

    private static void AddErrorTransport(MethodModel method, CallShape shape)
    {
        if (method.NoExceptions)
        {
            return;
        }

        shape.NativeParameterTypes.Add("global::TedToolkit.CppBindings.NativeError*");
        shape.NativeArguments.Add("&__error");
    }

    private static string ErrorDeclaration(MethodModel method)
    {
        return method.NoExceptions
            ? ""
            : "global::TedToolkit.CppBindings.NativeError __error = default;\n";
    }

    private void AddErrorCheck(MethodModel method, List<string> statements)
    {
        if (method.NoExceptions)
        {
            return;
        }

        statements.Add(
            profile.ManagedNativeErrorProjection + ".ThrowIfFailed(\n"
            + "    ref __error,\n"
            + "    (delegate* unmanaged[Cdecl]<global::TedToolkit.CppBindings.NativeError*, void>)"
            + GetNativeFunctionExpression(profile.NativeErrorClearExport) + ");");
    }

    private void AddReceiverKeepAlive(
        MethodModel method,
        bool borrowedHandleReceiver,
        List<string> statements)
    {
        if (method.IsStatic || borrowedHandleReceiver
            || record.ObjectKind is not (NativeObjectKind.IntrusiveHandle or NativeObjectKind.Owned))
        {
            return;
        }

        statements.Add("GC.KeepAlive(self);");
    }

    private static IEnumerable<string> KeepAliveStatements(IEnumerable<string> owners)
    {
        return owners.Distinct(StringComparer.Ordinal).Select(static owner => $"GC.KeepAlive({owner});");
    }

    private static void AddPublicParameters(Method declaration, CallShape shape)
    {
        foreach (var parameter in shape.PublicParameters)
        {
            declaration.AddParameter(parameter);
        }
    }

    private string GetDeleteExportName()
    {
        return record.MethodModels.Single(static method => method.Type is MethodModelType.DELETE)
            .NativeExportName;
    }

    private string GetNativeFunctionExpression(string exportName)
    {
        if (nativeFunctionIndices is null || !nativeFunctionIndices.TryGetValue(exportName, out var index))
        {
            throw new InvalidOperationException($"Native function '{exportName}' has no function-table index.");
        }

        return $"global::{profile.CSharpNamespace}.NativeApi.GetFunction({index})";
    }

    private static string Invoke(
        string exportName,
        IEnumerable<string> nativeParameterTypes,
        IEnumerable<string> nativeArguments,
        Func<string, string>? resolveExport = null)
    {
        var target = resolveExport?.Invoke(exportName) ?? exportName;
        return "((delegate* unmanaged[Cdecl]<" + string.Join(", ", nativeParameterTypes) + ">)"
               + target + ")(" + string.Join(", ", nativeArguments) + ");";
    }

    private string Invoke(
        string exportName,
        IEnumerable<string> nativeParameterTypes,
        IEnumerable<string> nativeArguments)
    {
        return Invoke(exportName, nativeParameterTypes, nativeArguments, GetNativeFunctionExpression);
    }

    private string InvokeVoid(
        string exportName,
        IEnumerable<string> nativeParameterTypes,
        IEnumerable<string> nativeArguments)
    {
        return Invoke(exportName, nativeParameterTypes.Append("void"), nativeArguments);
    }

    private static string ComposeFixedBlock(
        IReadOnlyList<(string Type, string Name, string Expression)> pins,
        IEnumerable<string> statements)
    {
        var lines = pins.Select(static pin => $"fixed ({pin.Type}* {pin.Name} = {pin.Expression})")
            .Append("{")
            .Concat(statements.Select(static statement => Indent(statement, 4)))
            .Append("}");
        return JoinLines(lines);
    }

    private static List<(string Type, string Name, string Expression)> PrependPin(
        (string Type, string Name, string Expression) pin,
        List<(string Type, string Name, string Expression)> pins)
    {
        var result = new List<(string Type, string Name, string Expression)>(pins.Count + 1) { pin, };
        result.AddRange(pins);
        return result;
    }

    private RecordModel[] GetDerivedRecords()
    {
        return recordCatalog?.Values
                   .Where(candidate => !ReferenceEquals(candidate, record)
                                       && TryGetInheritancePath(candidate, record, out _))
                   .OrderBy(static candidate => candidate.Type.CSharpTypeName, StringComparer.Ordinal)
                   .ToArray()
               ?? Array.Empty<RecordModel>();
    }

    private string GetManagedInterfaceName(RecordModel value)
    {
        return value.Type.CSharpInterfaceName == profile.IntrusiveRootInterfaceName
            ? profile.ManagedIntrusiveRootInterface
            : value.Type.CSharpInterfaceName;
    }

    private static bool TryGetInheritancePath(
        RecordModel derived,
        RecordModel target,
        out IReadOnlyList<(RecordModel Derived, BaseRelationModel Relation)> path)
    {
        foreach (var relation in derived.Bases.Where(static relation => relation.IsPublic))
        {
            if (ReferenceEquals(relation.Base, target))
            {
                path = [(derived, relation),];
                return true;
            }

            if (TryGetInheritancePath(relation.Base, target, out var tail))
            {
                path = [(derived, relation), .. tail,];
                return true;
            }
        }

        path = [];
        return false;
    }

    private static TypeIndirectionKind? GetFirstIndirection(TypeModel type)
    {
        return type.Transport.Indirections.Count is 0 ? null : type.Transport.Indirections[0].Kind;
    }

    private static bool IsReferencedValueConst(TypeModel type)
    {
        return type.Transport.Indirections.Count > 1
            ? type.Transport.Indirections[1].IsConstQualified
            : type.Transport.ValueIsConst;
    }

    private static DataType GetCSharpValueType(TypeModel type)
    {
        var source = type.CSharpPublicType;
        return new(source.Type)
        {
            IsArray = source.IsArray,
            PointCounter = source.PointCounter,
        };
    }

    private static string JoinLines(IEnumerable<string> lines)
    {
        return string.Join("\n", lines);
    }

    private static string Indent(string value, int spaces)
    {
        var indentation = new string(' ', spaces);
        return indentation + value.Replace("\n", "\n" + indentation, StringComparison.Ordinal);
    }

    private sealed class CallShape
    {
        internal List<Parameter> PublicParameters { get; } = [];

        internal List<string> NativeParameterTypes { get; } = [];

        internal List<string> NativeArguments { get; } = [];

        internal List<(string Type, string Name, string Expression)> Pins { get; } = [];

        internal List<string> ParameterOwners { get; } = [];
    }
}