// -----------------------------------------------------------------------
// <copyright file="CSharpGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.Occt.Generator.Generators.CSharpGenerator>;

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Produces the generated C# partial struct for a parsed OCCT record.
/// </summary>
/// <param name="recordDecl">The record declaration being generated.</param>
/// <param name="generationOptions">The generator options.</param>
/// <param name="recordCatalog">The completed record models used to classify record results.</param>
internal sealed class CSharpGenerator(
    RecordModel recordDecl,
    IOptions<GenerationOptions> generationOptions,
    IReadOnlyDictionary<string, RecordModel>? recordCatalog = null) : IGenerator
{
    private bool UsesGenericReceiver
    {
        get
        {
            return GetDerivedRecords().Length > 0;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var structName = recordDecl.Type.CSharpPublicType.ToCode();
        var usesExplicitLayout = UsesExplicitLayout();
        var nameSpace = NameSpace("TedToolkit.Occt");
        var structDeclaration = Struct(structName).Unsafe
            .AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                .AddArgument(Argument(recordDecl.Type.CppTypeName.ToLiteral())))
            .AddAttribute(Attribute<StructLayoutAttribute>()
                .AddArgument(Argument((usesExplicitLayout ? LayoutKind.Explicit : LayoutKind.Sequential).ToExpression()))
                .AddNamedArgument(nameof(StructLayoutAttribute.Size),
                    recordDecl.Size.ToLiteral()));
        AddRootDescriptions(structDeclaration, recordDecl.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        structDeclaration = generationOptions.Value.IsInternal
            ? structDeclaration.Internal
            : structDeclaration.Public;
        GenerateFields(structDeclaration, usesExplicitLayout);
        nameSpace.AddMember(structDeclaration);
        GenerateInterfaces(nameSpace, structDeclaration);

        var representation = File()
            .AddNameSpace(nameSpace)
            .ToCode();
        return representation + GenerateExtensions();
    }

    private void GenerateInterfaces(NameSpace nameSpace, TypeDeclaration? structDeclaration)
    {
        if (recordDecl.Type.CSharpInterfaceName is "IStandard_Transient")
        {
            structDeclaration?.AddBaseType(new DataType("global::TedToolkit.Occt.IStandard_Transient"));
            return;
        }

        var interfaceDeclaration = Interface(recordDecl.Type.CSharpInterfaceName).Public.Unsafe;
        structDeclaration?.AddBaseType(new DataType(recordDecl.Type.CSharpInterfaceName));
        foreach (var baseRelation in recordDecl.Bases.Where(relation =>
                     relation.IsPublic
                     && recordCatalog?.ContainsKey(relation.Base.Type.CppTypeName) is not false))
        {
            interfaceDeclaration.AddBaseType(new DataType(
                baseRelation.Base.Type.CSharpInterfaceName is "IStandard_Transient"
                    ? "global::TedToolkit.Occt.IStandard_Transient"
                    : baseRelation.Base.Type.CSharpInterfaceName));
        }

        if (recordDecl.ObjectKind is NativeObjectKind.Handle
            && !recordDecl.Bases.Any(static relation => relation.Base.IsStandardTransient))
        {
            interfaceDeclaration.AddBaseType(new DataType("global::TedToolkit.Occt.IStandard_Transient"));
        }
        else if (recordDecl.ObjectKind is NativeObjectKind.Owned)
        {
            interfaceDeclaration.AddBaseType(new DataType("global::TedToolkit.Occt.IOcctRaii"));
        }

        if (recordDecl.ObjectKind is NativeObjectKind.Handle
            && recordDecl.MethodModels.Any(static method =>
                method.Type is MethodModelType.VALUE_DELETE))
        {
            interfaceDeclaration.AddBaseType(new DataType("global::TedToolkit.Occt.IOcctRaii"));
        }

        nameSpace.AddMember(interfaceDeclaration);
    }

    private bool UsesExplicitLayout()
    {
        var currentOffset = 0L;
        foreach (var field in recordDecl.FieldModels.OrderBy(static field => field.Offset))
        {
            if (field.Offset < currentOffset)
            {
                return true;
            }

            currentOffset = checked(field.Offset + field.Size);
        }

        return false;
    }

    private void GenerateFields(TypeDeclaration structDeclaration, bool usesExplicitLayout)
    {
        if (usesExplicitLayout)
        {
            foreach (var field in recordDecl.FieldModels.OrderBy(static field => field.Offset))
            {
                AddField(structDeclaration, field, true);
            }

            return;
        }

        var currentOffset = 0L;
        var paddingIndex = 0;
        foreach (var fieldDecl in recordDecl.FieldModels.OrderBy(static field => field.Offset))
        {
            AddPadding(structDeclaration, ref currentOffset, fieldDecl.Offset, ref paddingIndex);
            AddField(structDeclaration, fieldDecl, false);
            currentOffset = checked(fieldDecl.Offset + fieldDecl.Size);
        }

        AddPadding(structDeclaration, ref currentOffset, recordDecl.Size, ref paddingIndex);
    }

    private static void AddField(TypeDeclaration structDeclaration, FieldModel fieldModel, bool usesExplicitLayout)
    {
        var field = Field(fieldModel.Type.CSharpPInvokeType, fieldModel.Name)
            .AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                .AddArgument(Argument(fieldModel.Type.CppTypeName.ToLiteral())))
            .Public;
        if (usesExplicitLayout)
        {
            field.AddAttribute(Attribute<FieldOffsetAttribute>()
                .AddArgument(Argument(fieldModel.Offset.ToLiteral())));
        }

        AddRootDescriptions(field, fieldModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        structDeclaration.AddMember(field);
    }

    private static void AddPadding(
        TypeDeclaration structDeclaration,
        ref long currentOffset,
        long targetOffset,
        ref int paddingIndex)
    {
        while (currentOffset < targetOffset)
        {
            structDeclaration.AddMember(Field(DataType.Byte, $"__padding{paddingIndex++}").Private);
            currentOffset++;
        }
    }

    private static void AddRootDescriptions<TTarget>(
        TTarget target,
        IReadOnlyList<IRootDescriptionItem> descriptions,
        Action<TTarget, IRootDescriptionItem> addDescription)
    {
        foreach (var description in descriptions)
        {
            addDescription(target, description);
        }
    }

    private string GenerateExtensions()
    {
        var methods = recordDecl.MethodModels.Where(method =>
                (method.Type is MethodModelType.NEW
                 && !recordDecl.IsAbstract
                 && (recordDecl.ObjectKind is NativeObjectKind.Value
                     || recordDecl.MethodModels.Any(static candidate => candidate.Type is MethodModelType.DELETE)))
                || (method.Type is MethodModelType.NORMAL
                && (GetFirstIndirection(method.ReturnType) is TypeIndirectionKind.LValueReference
                    || (method.ReturnType.IsOcctHandle
                        && method.ReturnType.Transport.Indirections.Count is 0)
                    || (method.ReturnType.IsRecord
                        && method.ReturnType.Transport.Indirections.Count is 0
                        && !method.ReturnType.IsOcctHandle)
                    || (!method.ReturnType.IsRecord
                        && method.ReturnType.Transport.Indirections.Count is 0))))
            .ToArray();
        if (methods.Length is 0)
        {
            return "";
        }

        var recordName = recordDecl.Type.CSharpTypeName;
        var builder = new StringBuilder("\n\nnamespace TedToolkit.Occt\n{\n    public static unsafe class ")
            .Append(recordName).Append("Extensions\n    {\n");
        foreach (var method in methods)
        {
            if (method.Type is MethodModelType.NEW)
            {
                AppendConstructorExtension(builder, method, recordName);
                continue;
            }

            AppendExtension(builder, method, recordName, borrowedHandleReceiver: false);
            if (!method.IsStatic && recordDecl.ObjectKind is NativeObjectKind.Handle)
            {
                AppendExtension(builder, method, recordName, borrowedHandleReceiver: true);
            }
        }

        if (UsesGenericReceiver)
        {
            AppendPointerAdjustmentHelper(builder, recordName);
        }

        return builder.Append("    }\n}\n").ToString();
    }

    private void AppendConstructorExtension(StringBuilder builder, MethodModel method, string recordName)
    {
        var publicParameters = new List<string>();
        var nativeParameterTypes = new List<string>();
        var nativeArguments = new List<string>();
        var pins = new List<(string Type, string Name, string Expression)>();
        var parameterOwners = new List<string>();
        foreach (var parameter in method.Parameters)
        {
            AppendParameter(parameter, publicParameters, nativeParameterTypes, nativeArguments, pins, parameterOwners);
        }

        if (recordDecl.ObjectKind is not NativeObjectKind.Handle)
        {
            nativeParameterTypes.Insert(0, recordName + "*");
            nativeArguments.Insert(0, "__resultPointer");
        }

        if (!method.NoExceptions)
        {
            nativeParameterTypes.Add("global::TedToolkit.Occt.NativeError*");
            nativeArguments.Add("&__error");
        }

        var returnType = recordDecl.ObjectKind switch
        {
            NativeObjectKind.Handle => $"global::TedToolkit.Occt.Handle<{recordName}>",
            NativeObjectKind.Owned => $"global::TedToolkit.Occt.Owned<{recordName}>",
            _ => recordName,
        };
        _ = builder.Append("        public static ").Append(returnType).Append(" Create(")
            .AppendJoin(", ", publicParameters).Append(")\n        {\n");
        if (!method.NoExceptions)
        {
            _ = builder.Append("            global::TedToolkit.Occt.NativeError __error = default;\n");
        }

        if (recordDecl.ObjectKind is NativeObjectKind.Handle)
        {
            AppendHandleConstructorBody(
                builder, method, recordName, nativeParameterTypes, nativeArguments, pins, parameterOwners);
            return;
        }

        if (recordDecl.ObjectKind is NativeObjectKind.Owned)
        {
            var deleteExport = GetDeleteExportName();
            _ = builder.Append("            var __result = new global::TedToolkit.Occt.Owned<")
                .Append(recordName).Append(">((delegate* unmanaged[Cdecl]<")
                .Append(recordName).Append("*, void>)global::TedToolkit.Occt.NativeApi.")
                .Append(deleteExport).Append(");\n")
                .Append("            var constructed = false;\n            try\n            {\n")
                .Append("                fixed (").Append(recordName)
                .Append("* __resultPointer = &__result.Value)\n");
            AppendPlacementConstructionCall(
                builder, method, nativeParameterTypes, nativeArguments, pins, parameterOwners, 4);
            _ = builder.Append("                constructed = true;\n                return __result;\n")
                .Append("            }\n            finally\n            {\n")
                .Append("                if (!constructed)\n                {\n")
                .Append("                    GC.SuppressFinalize(__result);\n                }\n")
                .Append("            }\n        }\n\n");
            return;
        }

        _ = builder.Append("            ").Append(recordName).Append(" __result = default;\n")
            .Append("            ").Append(recordName).Append("* __resultPointer = &__result;\n");
        AppendPlacementConstructionCall(
            builder, method, nativeParameterTypes, nativeArguments, pins, parameterOwners, 3);
        _ = builder.Append("            return __result;\n        }\n\n");
    }

    private void AppendHandleConstructorBody(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        List<string> nativeParameterTypes,
        List<string> nativeArguments,
        List<(string Type, string Name, string Expression)> pins,
        IReadOnlyList<string> parameterOwners)
    {
        foreach (var pin in pins)
        {
            _ = builder.Append("            fixed (").Append(pin.Type).Append("* ").Append(pin.Name)
                .Append(" = ").Append(pin.Expression).Append(")\n");
        }

        nativeParameterTypes.Add(recordName + "*");
        _ = builder.Append("            {\n                var __result = ((delegate* unmanaged[Cdecl]<")
            .AppendJoin(", ", nativeParameterTypes).Append(">)global::TedToolkit.Occt.NativeApi.")
            .Append(method.NativeExportName).Append(")(").AppendJoin(", ", nativeArguments)
            .Append(");\n");
        AppendKeepAlives(builder, parameterOwners, "                ");
        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder);
        }

        _ = builder.Append("                return new global::TedToolkit.Occt.Handle<")
            .Append(recordName).Append(">(__result, (delegate* unmanaged[Cdecl]<")
            .Append(recordName).Append("*, void>)global::TedToolkit.Occt.NativeApi.")
            .Append(GetDeleteExportName()).Append(");\n            }\n        }\n\n");
    }

    private static void AppendPlacementConstructionCall(
        StringBuilder builder,
        MethodModel method,
        IReadOnlyList<string> nativeParameterTypes,
        IReadOnlyList<string> nativeArguments,
        IReadOnlyList<(string Type, string Name, string Expression)> pins,
        IReadOnlyList<string> parameterOwners,
        int indentation)
    {
        var spaces = new string(' ', indentation * 4);
        foreach (var pin in pins)
        {
            _ = builder.Append(spaces).Append("fixed (").Append(pin.Type).Append("* ").Append(pin.Name)
                .Append(" = ").Append(pin.Expression).Append(")\n");
        }

        _ = builder.Append(spaces).Append("{\n").Append(spaces).Append("    ((delegate* unmanaged[Cdecl]<")
            .AppendJoin(", ", nativeParameterTypes.Append("void"))
            .Append(">)global::TedToolkit.Occt.NativeApi.").Append(method.NativeExportName).Append(")(")
            .AppendJoin(", ", nativeArguments).Append(");\n");
        AppendKeepAlives(builder, parameterOwners, spaces + "    ");
        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder, spaces + "    ");
        }

        _ = builder.Append(spaces).Append("}\n");
    }

    private string GetDeleteExportName()
    {
        return recordDecl.MethodModels.Single(static method => method.Type is MethodModelType.DELETE)
            .NativeExportName;
    }

    private void AppendExtension(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        bool borrowedHandleReceiver)
    {
        if (GetFirstIndirection(method.ReturnType) is TypeIndirectionKind.LValueReference)
        {
            AppendReferenceReturnExtension(builder, method, recordName, borrowedHandleReceiver);
        }
        else if (method.ReturnType.IsOcctHandle)
        {
            AppendHandleReturnExtension(builder, method, recordName, borrowedHandleReceiver);
        }
        else if (method.ReturnType.IsRecord)
        {
            AppendRecordReturnExtension(builder, method, recordName, borrowedHandleReceiver);
        }
        else
        {
            AppendScalarExtension(builder, method, recordName, borrowedHandleReceiver);
        }
    }

    private void AppendRecordReturnExtension(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        bool borrowedHandleReceiver)
    {
        if (recordCatalog is null
            || !recordCatalog.TryGetValue(method.ReturnType.CppValueTypeName, out var resultRecord))
        {
            throw new NotSupportedException(
                $"Record result '{method.ReturnType.CppValueTypeName}' has no completed model.");
        }

        var publicParameters = new List<string>();
        var nativeParameterTypes = new List<string>();
        var nativeArguments = new List<string>();
        var pins = new List<(string Type, string Name, string Expression)>();
        var parameterOwners = new List<string>();
        if (!method.IsStatic)
        {
            AppendReceiver(
                recordName,
                method,
                borrowedHandleReceiver,
                publicParameters,
                nativeParameterTypes,
                nativeArguments,
                pins);
        }

        foreach (var parameter in method.Parameters)
        {
            AppendParameter(parameter, publicParameters, nativeParameterTypes, nativeArguments, pins, parameterOwners);
        }

        var resultName = resultRecord.Type.CSharpTypeName;
        nativeParameterTypes.Add(resultName + "*");
        nativeArguments.Add("__resultPointer");
        if (!method.NoExceptions)
        {
            nativeParameterTypes.Add("global::TedToolkit.Occt.NativeError*");
            nativeArguments.Add("&__error");
        }

        var returnsOwnedValue = resultRecord.ObjectKind is NativeObjectKind.Owned or NativeObjectKind.Handle;
        var returnType = returnsOwnedValue
            ? $"global::TedToolkit.Occt.Owned<{resultName}>"
            : resultName;
        AppendMethodDeclarationStart(builder, returnType, method);
        _ = builder.AppendJoin(", ", publicParameters).Append(')');
        AppendReceiverConstraint(builder, method);
        _ = builder.Append("\n        {\n");
        if (!method.NoExceptions)
        {
            _ = builder.Append("            global::TedToolkit.Occt.NativeError __error = default;\n");
        }

        if (returnsOwnedValue)
        {
            var destructorType = resultRecord.ObjectKind is NativeObjectKind.Handle
                ? MethodModelType.VALUE_DELETE
                : MethodModelType.DELETE;
            var destructor = resultRecord.MethodModels.FirstOrDefault(value => value.Type == destructorType);
            if (destructor is null)
            {
                throw new NotSupportedException(
                    $"Method '{recordDecl.Type.CppTypeName}::{method.MethodName}' returns nontrivial "
                    + $"'{resultRecord.Type.CppTypeName}' by value, but that type has no callable destructor.");
            }

            var destroy = destructor.NativeExportName;
            _ = builder.Append("            var __result = new global::TedToolkit.Occt.Owned<")
                .Append(resultName).Append(">((delegate* unmanaged[Cdecl]<")
                .Append(resultName).Append("*, void>)global::TedToolkit.Occt.NativeApi.")
                .Append(destroy).Append(");\n")
                .Append("            var constructed = false;\n            try\n            {\n")
                .Append("                fixed (").Append(resultName)
                .Append("* __resultPointer = &__result.Value)\n");
            AppendRecordReturnCall(
                builder,
                method,
                nativeParameterTypes,
                nativeArguments,
                pins,
                parameterOwners,
                borrowedHandleReceiver,
                indentation: "                ");
            _ = builder.Append("                constructed = true;\n                return __result;\n")
                .Append("            }\n            finally\n            {\n")
                .Append("                if (!constructed)\n                {\n")
                .Append("                    GC.SuppressFinalize(__result);\n                }\n")
                .Append("            }\n        }\n\n");
            return;
        }

        _ = builder.Append("            ").Append(resultName).Append(" __result = default;\n")
            .Append("            ").Append(resultName).Append("* __resultPointer = &__result;\n");
        AppendRecordReturnCall(
            builder,
            method,
            nativeParameterTypes,
            nativeArguments,
            pins,
            parameterOwners,
            borrowedHandleReceiver,
            indentation: "            ");
        _ = builder.Append("            return __result;\n        }\n\n");
    }

    private void AppendRecordReturnCall(
        StringBuilder builder,
        MethodModel method,
        IReadOnlyList<string> nativeParameterTypes,
        IReadOnlyList<string> nativeArguments,
        IReadOnlyList<(string Type, string Name, string Expression)> pins,
        IReadOnlyList<string> parameterOwners,
        bool borrowedHandleReceiver,
        string indentation)
    {
        foreach (var pin in pins)
        {
            _ = builder.Append(indentation).Append("fixed (").Append(pin.Type).Append("* ")
                .Append(pin.Name).Append(" = ").Append(pin.Expression).Append(")\n");
        }

        _ = builder.Append(indentation).Append("{\n").Append(indentation)
            .Append("    ((delegate* unmanaged[Cdecl]<")
            .AppendJoin(", ", nativeParameterTypes.Append("void"))
            .Append(">)global::TedToolkit.Occt.NativeApi.").Append(method.NativeExportName)
            .Append(")(").AppendJoin(", ", nativeArguments).Append(");\n");
        if (!method.IsStatic && !borrowedHandleReceiver
            && recordDecl.ObjectKind is NativeObjectKind.Handle or NativeObjectKind.Owned)
        {
            _ = builder.Append(indentation).Append("    GC.KeepAlive(self);\n");
        }

        AppendKeepAlives(builder, parameterOwners, indentation + "    ");

        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder, indentation + "    ");
        }

        _ = builder.Append(indentation).Append("}\n");
    }

    private void AppendHandleReturnExtension(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        bool borrowedHandleReceiver)
    {
        var publicParameters = new List<string>();
        var nativeParameterTypes = new List<string>();
        var nativeArguments = new List<string>();
        var pins = new List<(string Type, string Name, string Expression)>();
        var parameterOwners = new List<string>();
        if (!method.IsStatic)
        {
            AppendReceiver(
                recordName,
                method,
                borrowedHandleReceiver,
                publicParameters,
                nativeParameterTypes,
                nativeArguments,
                pins);
        }

        foreach (var parameter in method.Parameters)
        {
            AppendParameter(parameter, publicParameters, nativeParameterTypes, nativeArguments, pins, parameterOwners);
        }

        if (!method.NoExceptions)
        {
            nativeParameterTypes.Add("global::TedToolkit.Occt.NativeError*");
            nativeArguments.Add("&__error");
        }

        var elementType = method.ReturnType.OcctHandleElementType;
        AppendMethodDeclarationStart(
            builder,
            $"global::TedToolkit.Occt.Handle<{elementType}>?",
            method);
        _ = builder.AppendJoin(", ", publicParameters).Append(')');
        AppendReceiverConstraint(builder, method);
        _ = builder.Append("\n        {\n");
        if (!method.NoExceptions)
        {
            _ = builder.Append("            global::TedToolkit.Occt.NativeError __error = default;\n");
        }

        foreach (var pin in pins)
        {
            _ = builder.Append("            fixed (").Append(pin.Type).Append("* ").Append(pin.Name)
                .Append(" = ").Append(pin.Expression).Append(")\n");
        }

        nativeParameterTypes.Add(elementType + "*");
        _ = builder.Append("            {\n                var __result = ((delegate* unmanaged[Cdecl]<")
            .AppendJoin(", ", nativeParameterTypes).Append(">)global::TedToolkit.Occt.NativeApi.")
            .Append(method.NativeExportName).Append(")(").AppendJoin(", ", nativeArguments)
            .Append(");\n");
        if (!method.IsStatic && !borrowedHandleReceiver
            && recordDecl.ObjectKind is NativeObjectKind.Handle or NativeObjectKind.Owned)
        {
            _ = builder.Append("                GC.KeepAlive(self);\n");
        }

        AppendKeepAlives(builder, parameterOwners, "                ");

        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder);
        }

        _ = builder.Append("                if (__result == null)\n                {\n")
            .Append("                    return null;\n                }\n\n")
            .Append("                return new global::TedToolkit.Occt.Handle<")
            .Append(elementType).Append(">(__result, (delegate* unmanaged[Cdecl]<")
            .Append(elementType).Append("*, void>)global::TedToolkit.Occt.NativeApi.")
            .Append(NativeExportNameBuilder.GetHandleReleaseName(method.ReturnType.OcctHandleElementCppType))
            .Append(");\n            }\n        }\n\n");
    }

    private void AppendScalarExtension(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        bool borrowedHandleReceiver)
    {
        var publicParameters = new List<string>();
        var nativeParameterTypes = new List<string>();
        var nativeArguments = new List<string>();
        var pins = new List<(string Type, string Name, string Expression)>();
        var parameterOwners = new List<string>();
        if (!method.IsStatic)
        {
            AppendReceiver(
                recordName,
                method,
                borrowedHandleReceiver,
                publicParameters,
                nativeParameterTypes,
                nativeArguments,
                pins);
        }

        foreach (var parameter in method.Parameters)
        {
            AppendParameter(parameter, publicParameters, nativeParameterTypes, nativeArguments, pins, parameterOwners);
        }

        if (!method.NoExceptions)
        {
            nativeParameterTypes.Add("global::TedToolkit.Occt.NativeError*");
            nativeArguments.Add("&__error");
        }

        var returnType = method.ReturnType.CSharpPublicType.ToCode();
        AppendMethodDeclarationStart(builder, returnType, method);
        _ = builder.AppendJoin(", ", publicParameters).Append(')');
        AppendReceiverConstraint(builder, method);
        _ = builder.Append("\n        {\n");
        if (!method.NoExceptions)
        {
            _ = builder.Append("            global::TedToolkit.Occt.NativeError __error = default;\n");
        }

        foreach (var pin in pins)
        {
            _ = builder.Append("            fixed (").Append(pin.Type).Append("* ").Append(pin.Name)
                .Append(" = ").Append(pin.Expression).Append(")\n");
        }

        _ = builder.Append("            {\n                ");
        if (!method.IsReturnVoid)
        {
            _ = builder.Append("var __result = ");
        }

        var signatureTypes = nativeParameterTypes.Append(method.ReturnType.CSharpPInvokeType.ToCode());
        _ = builder.Append("((delegate* unmanaged[Cdecl]<").AppendJoin(", ", signatureTypes)
            .Append(">)global::TedToolkit.Occt.NativeApi.").Append(method.NativeExportName).Append(")(")
            .AppendJoin(", ", nativeArguments).Append(");\n");
        if (!method.IsStatic && !borrowedHandleReceiver
            && recordDecl.ObjectKind is NativeObjectKind.Handle or NativeObjectKind.Owned)
        {
            _ = builder.Append("                GC.KeepAlive(self);\n");
        }

        AppendKeepAlives(builder, parameterOwners, "                ");

        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder);
        }

        if (!method.IsReturnVoid)
        {
            var pinvokeReturnType = method.ReturnType.CSharpPInvokeType.ToCode();
            _ = builder.Append("                return ");
            if (!string.Equals(returnType, pinvokeReturnType, StringComparison.Ordinal))
            {
                _ = builder.Append('(').Append(returnType).Append(')');
            }

            _ = builder.Append("__result;\n");
        }

        _ = builder.Append("            }\n        }\n\n");
    }

    private void AppendReferenceReturnExtension(
        StringBuilder builder,
        MethodModel method,
        string recordName,
        bool borrowedHandleReceiver)
    {
        var returnModifier = IsReferencedValueConst(method.ReturnType) ? "ref readonly " : "ref ";
        var returnTypeName = GetCSharpValueTypeName(method.ReturnType);
        _ = builder.Append("        /// <remarks>The returned reference is borrowed from native storage. ")
            .Append("The caller must preserve the native owner and obey the original C++ invalidation rules.</remarks>\n")
            .Append("        public static ").Append(returnModifier)
            .Append(returnTypeName).Append(' ').Append(GetPublicMethodName(method).ToValidIdentifier());
        if (!method.IsStatic && UsesGenericReceiver)
        {
            _ = builder.Append("<TReceiver>");
        }

        _ = builder.Append('(');

        var publicParameters = new List<string>();
        var nativeParameterTypes = new List<string>();
        var nativeArguments = new List<string>();
        var pins = new List<(string Type, string Name, string Expression)>();
        var parameterOwners = new List<string>();
        if (!method.IsStatic)
        {
            AppendReceiver(
                recordName,
                method,
                borrowedHandleReceiver,
                publicParameters,
                nativeParameterTypes,
                nativeArguments,
                pins);
        }

        foreach (var parameter in method.Parameters)
        {
            AppendParameter(parameter, publicParameters, nativeParameterTypes, nativeArguments, pins, parameterOwners);
        }

        if (!method.NoExceptions)
        {
            nativeParameterTypes.Add("global::TedToolkit.Occt.NativeError*");
            nativeArguments.Add("&__error");
        }

        _ = builder.AppendJoin(", ", publicParameters).Append(')');
        AppendReceiverConstraint(builder, method);
        _ = builder.Append("\n        {\n");
        if (!method.NoExceptions)
        {
            _ = builder.Append("            global::TedToolkit.Occt.NativeError __error = default;\n");
        }

        foreach (var pin in pins)
        {
            _ = builder.Append("            fixed (").Append(pin.Type).Append("* ").Append(pin.Name)
                .Append(" = ").Append(pin.Expression).Append(")\n");
        }

        _ = builder.Append("            {\n                var __result = ((delegate* unmanaged[Cdecl]<");
        var signatureTypes = nativeParameterTypes
            .Append(returnTypeName + "*");
        _ = builder.AppendJoin(", ", signatureTypes).Append(">)")
            .Append("global::TedToolkit.Occt.NativeApi.").Append(method.NativeExportName).Append(")(")
            .AppendJoin(", ", nativeArguments).Append(");\n");
        if (!method.IsStatic && !borrowedHandleReceiver
            && recordDecl.ObjectKind is NativeObjectKind.Handle or NativeObjectKind.Owned)
        {
            _ = builder.Append("                GC.KeepAlive(self);\n");
        }

        AppendKeepAlives(builder, parameterOwners, "                ");

        if (!method.NoExceptions)
        {
            AppendErrorProjection(builder);
        }

        _ = builder.Append("                return ref *__result;\n            }\n        }\n\n");
    }

    private static void AppendErrorProjection(StringBuilder builder, string indentation = "                ")
    {
        _ = builder.Append(indentation).Append("global::TedToolkit.Occt.NativeErrorProjection.ThrowIfFailed(\n")
            .Append(indentation).Append("    ref __error,\n")
            .Append(indentation).Append("    (delegate* unmanaged[Cdecl]<global::TedToolkit.Occt.NativeError*, void>)")
            .Append("global::TedToolkit.Occt.NativeApi.NativeError_Clear);\n");
    }

    private void AppendReceiver(
        string recordName,
        MethodModel method,
        bool borrowedHandleReceiver,
        List<string> publicParameters,
        List<string> nativeParameterTypes,
        List<string> nativeArguments,
        List<(string Type, string Name, string Expression)> pins)
    {
        var receiverType = UsesGenericReceiver ? "TReceiver" : recordName;
        var publicReceiver = borrowedHandleReceiver
            ? GetBorrowedHandleReceiver(receiverType)
            : recordDecl.ObjectKind switch
            {
                NativeObjectKind.Handle or NativeObjectKind.Owned =>
                    $"this global::TedToolkit.Occt.IOcctOwner<{receiverType}> self",
                _ when UsesGenericReceiver => $"this ref {receiverType} self",
                _ => method.IsConst ? $"this in {receiverType} self" : $"this ref {receiverType} self",
            };
        publicParameters.Add(publicReceiver);
        nativeParameterTypes.Add(recordName + "*");
        nativeArguments.Add(UsesGenericReceiver ? "AdjustReceiver(selfPointer)" : "selfPointer");
        pins.Add((receiverType, "selfPointer",
            borrowedHandleReceiver || recordDecl.ObjectKind is NativeObjectKind.Handle or NativeObjectKind.Owned
                ? "&self.Value"
                : "&self"));
    }

    private string GetBorrowedHandleReceiver(string receiverType)
    {
        return UsesGenericReceiver
            ? $"this ref global::TedToolkit.Occt.handle<{receiverType}> self"
            : $"this in global::TedToolkit.Occt.handle<{receiverType}> self";
    }

    private void AppendMethodDeclarationStart(StringBuilder builder, string returnType, MethodModel method)
    {
        _ = builder.Append("        public static ").Append(returnType).Append(' ')
            .Append(GetPublicMethodName(method).ToValidIdentifier());
        if (!method.IsStatic && UsesGenericReceiver)
        {
            _ = builder.Append("<TReceiver>");
        }

        _ = builder.Append('(');
    }

    private void AppendReceiverConstraint(StringBuilder builder, MethodModel method)
    {
        if (method.IsStatic || !UsesGenericReceiver)
        {
            return;
        }

        _ = builder.Append("\n            where TReceiver : unmanaged, ")
            .Append(recordDecl.Type.CSharpInterfaceName);
        if (recordDecl.ObjectKind is NativeObjectKind.Handle
            && recordDecl.Type.CSharpInterfaceName is not "IStandard_Transient")
        {
            _ = builder.Append(", global::TedToolkit.Occt.IStandard_Transient");
        }
        else if (recordDecl.ObjectKind is NativeObjectKind.Owned)
        {
            _ = builder.Append(", global::TedToolkit.Occt.IOcctRaii");
        }
    }

    private string GetPublicMethodName(MethodModel method)
    {
        if (!method.IsStatic || method.Parameters.Count is 0)
        {
            return method.MethodName;
        }

        var conflictsWithInstance = recordDecl.MethodModels.Any(candidate =>
            !candidate.IsStatic
            && string.Equals(candidate.MethodName, method.MethodName, StringComparison.Ordinal)
            && candidate.Parameters.Count + 1 == method.Parameters.Count
            && string.Equals(
                method.Parameters[0].Type.CppValueTypeName,
                recordDecl.Type.CppTypeName,
                StringComparison.Ordinal)
            && candidate.Parameters.Select(static parameter => parameter.Type.CSharpPInvokeType.ToCode())
                .SequenceEqual(method.Parameters.Skip(1)
                    .Select(static parameter => parameter.Type.CSharpPInvokeType.ToCode()),
                    StringComparer.Ordinal));
        return conflictsWithInstance ? method.MethodName + "_1" : method.MethodName;
    }

    private RecordModel[] GetDerivedRecords()
    {
        return recordCatalog?.Values
                   .Where(candidate => !ReferenceEquals(candidate, recordDecl)
                                       && TryGetInheritancePath(candidate, recordDecl, out _))
                   .OrderBy(static candidate => candidate.Type.CSharpTypeName, StringComparer.Ordinal)
                   .ToArray()
               ?? Array.Empty<RecordModel>();
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

    private void AppendPointerAdjustmentHelper(StringBuilder builder, string recordName)
    {
        _ = builder.Append("        private static ").Append(recordName)
            .Append("* AdjustReceiver<TReceiver>(TReceiver* pointer)\n")
            .Append("            where TReceiver : unmanaged, ")
            .Append(recordDecl.Type.CSharpInterfaceName).Append("\n        {\n")
            .Append("            if (typeof(TReceiver) == typeof(").Append(recordName)
            .Append("))\n            {\n                return (").Append(recordName)
            .Append("*)pointer;\n            }\n");

        foreach (var derived in GetDerivedRecords())
        {
            if (!TryGetInheritancePath(derived, recordDecl, out var path))
            {
                continue;
            }

            _ = builder.Append("\n            if (typeof(TReceiver) == typeof(")
                .Append(derived.Type.CSharpTypeName).Append("))\n            {\n");
            if (path.All(static step => step.Relation.PointerAdjustment is PointerAdjustmentKind.Identity))
            {
                _ = builder.Append("                return (").Append(recordName).Append("*)(")
                    .Append(derived.Type.CSharpTypeName).Append("*)pointer;\n            }\n");
                continue;
            }

            var pointerExpression = $"({derived.Type.CSharpTypeName}*)pointer";
            foreach (var step in path)
            {
                pointerExpression = step.Relation.PointerAdjustment is PointerAdjustmentKind.Identity
                    ? $"({step.Relation.Base.Type.CSharpTypeName}*){pointerExpression}"
                    : $"((delegate* unmanaged[Cdecl]<{step.Derived.Type.CSharpTypeName}*, "
                      + $"{step.Relation.Base.Type.CSharpTypeName}*>)global::TedToolkit.Occt.NativeApi."
                      + NativeExportNameBuilder.GetPointerAdjustmentName(step.Derived, step.Relation.Base)
                      + $")({pointerExpression})";
            }

            _ = builder.Append("                return ").Append(pointerExpression)
                .Append(";\n            }\n");
        }

        _ = builder.Append("\n            throw new global::System.NotSupportedException(\n")
            .Append("                $\"No native pointer adjustment is generated from ")
            .Append("{typeof(TReceiver)} to ").Append(recordName).Append(".\");\n")
            .Append("        }\n\n");
    }

    private void AppendParameter(
        ParameterModel parameter,
        List<string> publicParameters,
        List<string> nativeParameterTypes,
        List<string> nativeArguments,
        List<(string Type, string Name, string Expression)> pins,
        List<string> parameterOwners)
    {
        var parameterName = parameter.Name.ToValidIdentifier();
        var firstIndirection = GetFirstIndirection(parameter.Type);
        var isReference = firstIndirection is TypeIndirectionKind.LValueReference
            or TypeIndirectionKind.RValueReference;
        var isRecordValue = parameter.Type.IsRecord && parameter.Type.Transport.Indirections.Count is 0;
        if (!parameter.Type.IsOcctHandle
            && parameter.Type.IsRecord
            && recordCatalog is not null
            && recordCatalog.TryGetValue(parameter.Type.CppValueTypeName, out var parameterRecord)
            && parameterRecord.ObjectKind is NativeObjectKind.Owned or NativeObjectKind.Handle)
        {
            var typeName = parameterRecord.Type.CSharpTypeName;
            var ownerName = parameterRecord.ObjectKind is NativeObjectKind.Owned ? "Owned" : "Handle";
            publicParameters.Add(
                $"global::TedToolkit.Occt.{ownerName}<{typeName}> {parameterName}");
            nativeParameterTypes.Add(typeName + "*");
            var pointerName = parameterName + "Pointer";
            nativeArguments.Add(pointerName);
            pins.Add((typeName, pointerName, "&" + parameterName + ".Value"));
            parameterOwners.Add(parameterName);
            return;
        }

        if (isReference || isRecordValue)
        {
            var typeName = GetCSharpValueTypeName(parameter.Type);
            var modifier = IsReferencedValueConst(parameter.Type) || isRecordValue ? "in " : "ref ";
            publicParameters.Add(modifier + typeName + " " + parameterName);
            var pinvokeType = parameter.Type.CSharpPInvokeType.ToCode();
            nativeParameterTypes.Add(pinvokeType.EndsWith('*') ? pinvokeType : typeName + "*");
            var pointerName = parameterName + "Pointer";
            nativeArguments.Add(pointerName);
            pins.Add((typeName, pointerName, "&" + parameterName));
            return;
        }

        var publicType = parameter.Type.CSharpPublicType.ToCode();
        var nativeType = parameter.Type.CSharpPInvokeType.ToCode();
        if (firstIndirection is TypeIndirectionKind.Pointer
            && (publicType.StartsWith("ref ", StringComparison.Ordinal)
                || publicType.StartsWith("in ", StringComparison.Ordinal)
                || publicType.StartsWith("out ", StringComparison.Ordinal)))
        {
            var typeName = GetCSharpValueTypeName(parameter.Type);
            publicParameters.Add(publicType + " " + parameterName);
            nativeParameterTypes.Add(nativeType);
            var pointerName = parameterName + "Pointer";
            nativeArguments.Add(pointerName);
            pins.Add((typeName, pointerName, "&" + parameterName));
            return;
        }

        if (firstIndirection is TypeIndirectionKind.Pointer
            && publicType is "global::System.ReadOnlySpan<byte>")
        {
            publicParameters.Add(publicType + " " + parameterName);
            nativeParameterTypes.Add(nativeType);
            var pointerName = parameterName + "Pointer";
            nativeArguments.Add(pointerName);
            pins.Add(("byte", pointerName, parameterName));
            return;
        }

        publicParameters.Add(publicType + " " + parameterName);
        nativeParameterTypes.Add(nativeType);
        nativeArguments.Add(string.Equals(publicType, nativeType, StringComparison.Ordinal)
            ? parameterName
            : $"({nativeType}){parameterName}");
    }

    private static void AppendKeepAlives(
        StringBuilder builder,
        IEnumerable<string> owners,
        string indentation)
    {
        foreach (var owner in owners.Distinct(StringComparer.Ordinal))
        {
            _ = builder.Append(indentation).Append("GC.KeepAlive(").Append(owner).Append(");\n");
        }
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

    private static string GetCSharpValueTypeName(TypeModel type)
    {
        var value = type.CSharpPublicType.ToCode();
        foreach (var prefix in new[] { "ref readonly ", "ref ", "in ", "out ", })
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return value[prefix.Length..];
            }
        }

        return value;
    }
}