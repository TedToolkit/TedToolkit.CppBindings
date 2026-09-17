// -----------------------------------------------------------------------
// <copyright file="BindingFiniteProfileManagedComposer.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Composes the managed half of a finite provider profile.
/// </summary>
/// <param name="api">The finite-profile API.</param>
/// <param name="profile">The provider emission profile.</param>
/// <param name="slots">The resolved native export slots.</param>
internal sealed class BindingFiniteProfileManagedComposer(
    BindingFiniteProfileApi api,
    BindingEmissionProfile profile,
    IReadOnlyDictionary<string, int> slots)
{
    /// <summary>
    /// Composes the complete managed source tree.
    /// </summary>
    /// <returns>The composed source file.</returns>
    internal SourceFile Compose()
    {
        var nameSpace = NameSpace(profile.CSharpNamespace);
        foreach (var status in api.Statuses)
        {
            nameSpace.AddMember(ComposeStatus(status));
        }

        foreach (var value in api.ValueTypes)
        {
            nameSpace.AddMember(ComposeValueType(value));
        }

        foreach (var result in api.OwnedResults)
        {
            nameSpace.AddMember(ComposeOwnedResult(result));
        }

        foreach (var result in api.CompositeResults)
        {
            nameSpace.AddMember(ComposeCompositeResult(result));
        }

        foreach (var owner in api.Owners)
        {
            nameSpace.AddMember(ComposeOwner(owner));
        }

        foreach (var group in api.Operations
                     .Where(static operation => operation is not BindingBufferOwnerOperationDefinition)
                     .GroupBy(GetContainingType, StringComparer.Ordinal))
        {
            var declaration = new TypeDeclaration(group.Key, TypeDeclarationType.CLASS).Public.Static.Unsafe;
            foreach (var operation in group)
            {
                declaration.AddMember(ComposeOperation(operation));
            }

            nameSpace.AddMember(declaration);
        }

        return File().AddNameSpace(nameSpace);
    }

    private static TedToolkit.RoslynHelper.Generators.Syntaxes.Enum ComposeStatus(BindingStatusDefinition status)
    {
        var declaration = new TedToolkit.RoslynHelper.Generators.Syntaxes.Enum(status.Name, DataType.Int).Public;
        foreach (var member in status.Members)
        {
            declaration.AddEnumMember(new EnumMember(member.Key, member.Value.ToLiteral()));
        }

        return declaration;
    }

    private static TypeDeclaration ComposeValueType(BindingValueTypeDefinition value)
    {
        var declaration = new TypeDeclaration(value.Name, TypeDeclarationType.STRUCT).Public.Readonly
            .AddAttribute(LayoutAttribute(value.Size, value.Alignment));
        var constructor = new Constructor()
        {
            Owner = value.Name,
            Accessibility = Accessibility.PUBLIC,
        };
        foreach (var field in value.Fields)
        {
            var storageName = ToCamelCase(field.Name);
            declaration.AddMember(new Field(new DataType(field.Type), storageName).Private.Readonly);
            constructor.AddParameter(Parameter(new DataType(field.Type), storageName));
            constructor.AddStatement(new Custom("this." + storageName + " = " + storageName + ";"));

            var property = new Property(new DataType(field.Type), field.Name).Public;
            property.AddAccessor(new Accessor(AccessorType.GET)
                .AddStatement(new Custom("return " + storageName + ";")));
            declaration.AddMember(property);
        }

        declaration.AddMember(constructor);
        return declaration;
    }

    private static TypeDeclaration ComposeOwnedResult(BindingOwnedResultDefinition result)
    {
        var declaration = new TypeDeclaration(result.Name, TypeDeclarationType.CLASS).Public.Sealed;
        var ownerType = "global::TedToolkit.CppBindings.Owned<" + result.OwnerName + ">?";
        var constructor = new Constructor()
        {
            Owner = result.Name,
            Accessibility = Accessibility.INTERNAL,
        };
        constructor.AddParameter(Parameter(new DataType(result.StatusName), "code"));
        constructor.AddParameter(Parameter(new DataType(ownerType), "model"));
        constructor.AddStatement(new Custom(result.StatusProperty + " = code;"));
        constructor.AddStatement(new Custom(result.OwnerProperty + " = model;"));
        declaration.AddMember(constructor);
        declaration.AddMember(AutoProperty(result.StatusName, result.StatusProperty));
        declaration.AddMember(AutoProperty(ownerType, result.OwnerProperty));
        return declaration;
    }

    private static TypeDeclaration ComposeCompositeResult(BindingCompositeResultDefinition result)
    {
        if (result.Kind is BindingCompositeResultKind.RecordStruct)
        {
            var record = new TypeDeclaration(result.Name, TypeDeclarationType.RECORD_STRUCT).Public.Readonly;
            foreach (var field in result.Fields)
            {
                record.AddParameter(Parameter(new DataType(field.ManagedType), field.Name));
            }

            return record;
        }

        var declaration = new TypeDeclaration(result.Name, TypeDeclarationType.CLASS).Public.Sealed;
        var constructor = new Constructor()
        {
            Owner = result.Name,
            Accessibility = Accessibility.INTERNAL,
        };
        foreach (var field in result.Fields)
        {
            var parameterName = ToCamelCase(field.Name);
            constructor.AddParameter(Parameter(new DataType(field.ManagedType), parameterName));
            constructor.AddStatement(new Custom(field.Name + " = " + parameterName + ";"));
            declaration.AddMember(AutoProperty(field.ManagedType, field.Name));
        }

        declaration.AddMember(constructor);
        foreach (var property in result.ComputedProperties ?? Array.Empty<BindingComputedPropertyDefinition>())
        {
            var declarationProperty = new Property(new DataType(property.Type), property.Name).Public;
            declarationProperty.AddAccessor(new Accessor(AccessorType.GET)
                .AddStatement(new Custom("return " + property.Expression + ";")));
            declaration.AddMember(declarationProperty);
        }

        return declaration;
    }

    private TypeDeclaration ComposeOwner(BindingOwnerDefinition owner)
    {
        var declaration = new TypeDeclaration(owner.Name, TypeDeclarationType.STRUCT).Public.Unsafe
            .AddBaseType(new DataType("global::TedToolkit.CppBindings.ICppRaii"))
            .AddAttribute(LayoutAttribute(owner.Size, owner.Alignment))
            .AddMember(new Field(new DataType("nint"), "storage").Private);
        foreach (var operation in api.Operations.OfType<BindingBufferOwnerOperationDefinition>()
                     .Where(operation => operation.OwnerName == owner.Name))
        {
            declaration.AddMember(ComposeBufferOwnerOperation(owner, operation));
        }

        return declaration;
    }

    private Method ComposeBufferOwnerOperation(
        BindingOwnerDefinition owner,
        BindingBufferOwnerOperationDefinition operation)
    {
        var returnType = operation.OwnedResultName
            ?? "global::TedToolkit.CppBindings.Owned<" + owner.Name + ">";
        var declaration = new Method(operation.MethodName, ReturnType(new DataType(returnType))).Public.Static;
        foreach (var buffer in operation.Buffers)
        {
            declaration.AddParameter(Parameter(new DataType("ReadOnlySpan<" + buffer.ElementType + ">"), buffer.Name));
        }

        var statements = BufferValidation(operation.Buffers);
        statements.Add(OwnerConstruction(owner, ComposeBufferOwnerCall(owner, operation)));
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private List<string> ComposeBufferOwnerCall(
        BindingOwnerDefinition owner,
        BindingBufferOwnerOperationDefinition operation)
    {
        var statements = new List<string>() { "global::TedToolkit.CppBindings.NativeError error = default;", };
        if (operation.OwnedResultName is not null)
        {
            statements.Add(operation.NativeReturnType + " code;");
        }

        var types = new List<string>() { owner.Name + "*", };
        types.AddRange(operation.Buffers.SelectMany(static buffer =>
            new[] { buffer.ElementType + "*", "nuint", }));
        types.Add("global::TedToolkit.CppBindings.NativeError*");
        types.Add(operation.NativeReturnType);
        var arguments = new List<string>() { "resultPointer", };
        arguments.AddRange(operation.Buffers.SelectMany(buffer =>
            new[] { BufferPointerName(buffer), "(nuint)" + buffer.Name + ".Length", }));
        arguments.Add("&error");
        var invocation = Invoke(operation.NativeExport, types, arguments);
        if (operation.OwnedResultName is not null)
        {
            invocation = "code = " + invocation;
        }

        var callStatements = new List<string>() { invocation, ManagedErrorCheck(), };
        var pins = new List<(string Type, string Name, string Expression)>();
        pins.Add((owner.Name, "resultPointer", "&owner.Value"));
        pins.AddRange(operation.Buffers.Select(buffer =>
            (buffer.ElementType, BufferPointerName(buffer), buffer.Name)));
        statements.Add(ComposeFixedBlock(pins, callStatements));
        if (operation.OwnedResultName is not null)
        {
            statements.Add(
                "if (code != (int)" + operation.StatusName + "." + operation.SuccessMember + ")\n"
                + "{\n"
                + "    GC.SuppressFinalize(owner);\n"
                + "    return new " + operation.OwnedResultName + "((" + operation.StatusName + ")code, null);\n"
                + "}");
            statements.Add("constructed = true;");
            statements.Add(
                "return new " + operation.OwnedResultName + "(" + operation.StatusName + "."
                + operation.SuccessMember + ", owner);");
        }
        else
        {
            statements.Add("constructed = true;");
            statements.Add("return owner;");
        }

        return statements;
    }

    private Method ComposeOperation(BindingFiniteOperationDefinition operation)
    {
        return operation switch
        {
            BindingCompositeOperationDefinition composite => ComposeCompositeOperation(composite),
            BindingOwnedOperationDefinition owned => ComposeOwnedOperation(owned),
            BindingScalarOperationDefinition scalar => ComposeScalarOperation(scalar),
            BindingTwoPhaseOperationDefinition twoPhase => ComposeTwoPhaseOperation(twoPhase),
            _ => throw new InvalidOperationException("A buffer owner operation is emitted on its owner type."),
        };
    }

    private Method ComposeCompositeOperation(BindingCompositeOperationDefinition operation)
    {
        var result = Find(api.CompositeResults, operation.ResultName, static value => value.Name);
        var declaration = CreateMethod(operation.MethodName, result.Name, operation.Owners, operation.Values);
        var statements = ArgumentValidation(operation.Owners, operation.Values);
        statements.Add("global::TedToolkit.CppBindings.NativeError error = default;");
        statements.AddRange(result.Fields.Select(field =>
            field.NativeType + " " + ToCamelCase(field.Name) + " = " + field.InitialValue + ";"));

        var types = operation.Owners.Select(owner => owner.OwnerName + "*")
            .Concat(operation.Values.Select(ValueFunctionPointerType))
            .Concat(result.Fields.Select(static field => field.NativeType + "*"))
            .Append("global::TedToolkit.CppBindings.NativeError*")
            .Append("void");
        var arguments = operation.Owners.Select(OwnerPointerName)
            .Concat(operation.Values.Select(ValueArgument))
            .Concat(result.Fields.Select(field => "&" + ToCamelCase(field.Name)))
            .Append("&error");
        var callStatements = new List<string>() { Invoke(operation.NativeExport, types, arguments), };
        callStatements.AddRange(KeepAlive(operation.Owners));
        statements.Add(ComposeFixedBlock(OwnerPins(operation.Owners), callStatements));
        statements.Add(ManagedErrorCheck());
        statements.Add(
            "return new " + result.Name + "("
            + string.Join(", ", result.Fields.Select(field => field.Projection.Replace(
                "$value", ToCamelCase(field.Name), StringComparison.Ordinal))) + ");");
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private Method ComposeOwnedOperation(BindingOwnedOperationDefinition operation)
    {
        var owner = Find(api.Owners, operation.ResultOwnerName, static value => value.Name);
        var declaration = CreateMethod(
            operation.MethodName,
            "global::TedToolkit.CppBindings.Owned<" + owner.Name + ">",
            operation.Owners,
            operation.Values);
        var statements = ArgumentValidation(operation.Owners, operation.Values);
        var callStatements = new List<string>();
        callStatements.Add("global::TedToolkit.CppBindings.NativeError error = default;");
        var types = new[] { owner.Name + "*", }
            .Concat(operation.Owners.Select(parameter => parameter.OwnerName + "*"))
            .Concat(operation.Values.Select(ValueFunctionPointerType))
            .Append("global::TedToolkit.CppBindings.NativeError*")
            .Append("void");
        var arguments = new List<string>() { "resultPointer", }
            .Concat(operation.Owners.Select(OwnerPointerName))
            .Concat(operation.Values.Select(ValueArgument))
            .Append("&error");
        var invokeStatements = new List<string>() { Invoke(operation.NativeExport, types, arguments), };
        invokeStatements.AddRange(KeepAlive(operation.Owners));
        invokeStatements.Add(ManagedErrorCheck());
        var pins = new List<(string Type, string Name, string Expression)>();
        pins.Add((owner.Name, "resultPointer", "&owner.Value"));
        pins.AddRange(OwnerPins(operation.Owners));
        callStatements.Add(ComposeFixedBlock(pins, invokeStatements));
        callStatements.Add("constructed = true;");
        callStatements.Add("return owner;");
        statements.Add(OwnerConstruction(owner, callStatements));
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private Method ComposeScalarOperation(BindingScalarOperationDefinition operation)
    {
        var declaration = CreateMethod(
            operation.MethodName,
            operation.ManagedReturnType,
            operation.Owners,
            operation.Values);
        var statements = ArgumentValidation(operation.Owners, operation.Values);
        statements.Add("global::TedToolkit.CppBindings.NativeError error = default;");
        var transportType = operation.ManagedTransportType ?? operation.ManagedReturnType;
        var types = operation.Owners.Select(parameter => parameter.OwnerName + "*")
            .Concat(operation.Values.Select(ValueFunctionPointerType))
            .Append("global::TedToolkit.CppBindings.NativeError*")
            .Append(transportType);
        var arguments = operation.Owners.Select(OwnerPointerName)
            .Concat(operation.Values.Select(ValueArgument))
            .Append("&error");
        var callStatements = new List<string>();
        callStatements.Add("var result = " + Invoke(operation.NativeExport, types, arguments));
        callStatements.AddRange(KeepAlive(operation.Owners));
        callStatements.Add(ManagedErrorCheck());
        var cast = operation.ManagedReturnType == transportType ? "" : "(" + operation.ManagedReturnType + ")";
        callStatements.Add("return " + cast + "result;");
        statements.Add(ComposeFixedBlock(OwnerPins(operation.Owners), callStatements));
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private Method ComposeTwoPhaseOperation(BindingTwoPhaseOperationDefinition operation)
    {
        var owners = new List<BindingOwnerParameterDefinition>() { operation.Owner, };
        var declaration = CreateMethod(
            operation.MethodName,
            operation.ResultName,
            owners,
            Array.Empty<BindingValueParameterDefinition>());
        var statements = ArgumentValidation(owners, Array.Empty<BindingValueParameterDefinition>());
        statements.Add("global::TedToolkit.CppBindings.NativeError error = default;");
        statements.AddRange(operation.Buffers.Select(buffer => "nuint " + buffer.CountName + " = 0;"));

        var countTypes = new[] { operation.Owner.OwnerName + "*", }
            .Concat(operation.Buffers.Select(static _ => "nuint*"))
            .Append("global::TedToolkit.CppBindings.NativeError*")
            .Append("void");
        var countArguments = new[] { OwnerPointerName(operation.Owner), }
            .Concat(operation.Buffers.Select(buffer => "&" + buffer.CountName))
            .Append("&error");
        var countStatements = new List<string>() { Invoke(operation.CountExport, countTypes, countArguments), };
        countStatements.AddRange(KeepAlive(owners));
        statements.Add(ComposeFixedBlock(OwnerPins(owners), countStatements));
        statements.Add(ManagedErrorCheck());
        statements.Add(
            "if (" + string.Join(" || ", operation.Buffers.Select(buffer => buffer.CountName + " > int.MaxValue"))
            + ")\n{\n    throw new OverflowException(" + operation.OverflowMessage.ToLiteral().ToCode() + ");\n}");
        statements.AddRange(operation.Buffers.Select(buffer =>
            "var " + ToCamelCase(buffer.PropertyName) + " = new " + buffer.ElementType
            + "[(int)" + buffer.CountName + "];"));

        var copyTypes = new[] { operation.Owner.OwnerName + "*", }
            .Concat(operation.Buffers.SelectMany(static buffer =>
                new[] { buffer.ElementType + "*", "nuint", }))
            .Append("global::TedToolkit.CppBindings.NativeError*")
            .Append("void");
        var copyArguments = new[] { OwnerPointerName(operation.Owner), }
            .Concat(operation.Buffers.SelectMany(buffer =>
                new[] { BufferPointerName(buffer), buffer.CountName, }))
            .Append("&error");
        var copyStatements = new List<string>() { Invoke(operation.NativeExport, copyTypes, copyArguments), };
        copyStatements.AddRange(KeepAlive(owners));
        var copyPins = OwnerPins(owners);
        copyPins.AddRange(operation.Buffers.Select(buffer =>
            (buffer.ElementType, BufferPointerName(buffer), ToCamelCase(buffer.PropertyName))));
        statements.Add(ComposeFixedBlock(copyPins, copyStatements));
        statements.Add(ManagedErrorCheck());
        statements.Add(
            "return new " + operation.ResultName + "("
            + string.Join(", ", operation.Buffers.Select(buffer => ToCamelCase(buffer.PropertyName))) + ");");
        declaration.AddStatement(new Custom(JoinLines(statements)));
        return declaration;
    }

    private static Method CreateMethod(
        string methodName,
        string returnType,
        IReadOnlyList<BindingOwnerParameterDefinition> owners,
        IReadOnlyList<BindingValueParameterDefinition> values)
    {
        var declaration = new Method(methodName, ReturnType(new DataType(returnType))).Public.Static;
        foreach (var owner in owners)
        {
            var parameter = Parameter(
                new DataType("global::TedToolkit.CppBindings.Owned<" + owner.OwnerName + ">"),
                owner.Name);
            declaration.AddParameter(owner.IsExtensionReceiver ? parameter.This : parameter);
        }

        foreach (var value in values)
        {
            declaration.AddParameter(Parameter(new DataType(value.ManagedType), value.Name));
        }

        return declaration;
    }

    private static List<string> ArgumentValidation(
        IReadOnlyList<BindingOwnerParameterDefinition> owners,
        IReadOnlyList<BindingValueParameterDefinition> values)
    {
        var statements = owners.Select(owner => "ArgumentNullException.ThrowIfNull(" + owner.Name + ");").ToList();
        statements.AddRange(values.Where(static value => value.RequireDefinedEnum).Select(value =>
            "if (!Enum.IsDefined(" + value.Name + "))\n"
            + "{\n"
            + "    throw new ArgumentOutOfRangeException(nameof(" + value.Name + "));\n"
            + "}"));
        return statements;
    }

    private string OwnerConstruction(BindingOwnerDefinition owner, IReadOnlyList<string> body)
    {
        return "var owner = new global::TedToolkit.CppBindings.Owned<" + owner.Name + ">(\n"
               + "    (delegate* unmanaged[Cdecl]<" + owner.Name + "*, void>)NativeApi.GetFunction("
               + slots[owner.DestroyExport] + "));\n"
               + "var constructed = false;\n"
               + "try\n"
               + "{\n"
               + Indent(JoinLines(body), 4) + "\n"
               + "}\n"
               + "finally\n"
               + "{\n"
               + "    if (!constructed)\n"
               + "    {\n"
               + "        GC.SuppressFinalize(owner);\n"
               + "    }\n"
               + "}";
    }

    private static List<(string Type, string Name, string Expression)> OwnerPins(
        IReadOnlyList<BindingOwnerParameterDefinition> owners)
    {
        return owners.Select(owner =>
            (owner.OwnerName, OwnerPointerName(owner), "&" + owner.Name + ".Value")).ToList();
    }

    private static IEnumerable<string> KeepAlive(IEnumerable<BindingOwnerParameterDefinition> owners)
    {
        return owners.Select(owner => "GC.KeepAlive(" + owner.Name + ");");
    }

    private string ManagedErrorCheck()
    {
        return profile.ManagedNativeErrorProjection + ".ThrowIfFailed(\n"
               + "    ref error,\n"
               + "    (delegate* unmanaged[Cdecl]<global::TedToolkit.CppBindings.NativeError*, void>)"
               + "NativeApi.GetFunction(" + slots[profile.NativeErrorClearExport] + "));";
    }

    private static List<string> BufferValidation(IReadOnlyList<BindingBufferDefinition> buffers)
    {
        var statements = buffers.Select(buffer =>
            "if (" + buffer.Name + ".Length % " + buffer.ElementsPerItem + " != 0)\n"
            + "{\n"
            + "    throw new ArgumentException(" + buffer.LengthError.ToLiteral().ToCode() + ", nameof("
            + buffer.Name + "));\n"
            + "}").ToList();
        var indexed = buffers.FirstOrDefault(static buffer => buffer.RequiresIndicesBelowFirstBufferItemCount);
        if (indexed is null)
        {
            return statements;
        }

        var source = buffers[0];
        statements.Add("var itemCount = (nuint)(" + source.Name + ".Length / " + source.ElementsPerItem + ");");
        statements.Add(
            "foreach (var index in " + indexed.Name + ")\n"
            + "{\n"
            + "    if (index >= itemCount)\n"
            + "    {\n"
            + "        throw new ArgumentOutOfRangeException(nameof(" + indexed.Name + "), index,\n"
            + "            " + indexed.IndexError.ToLiteral().ToCode() + ");\n"
            + "    }\n"
            + "}");
        return statements;
    }

    private string Invoke(
        string exportName,
        IEnumerable<string> parameterTypes,
        IEnumerable<string> arguments)
    {
        return "((delegate* unmanaged[Cdecl]<" + string.Join(", ", parameterTypes) + ">)"
               + "NativeApi.GetFunction(" + slots[exportName] + "))("
               + string.Join(", ", arguments) + ");";
    }

    private static string ComposeFixedBlock(
        IReadOnlyList<(string Type, string Name, string Expression)> pins,
        IEnumerable<string> statements)
    {
        var lines = pins.Select(static pin => "fixed (" + pin.Type + "* " + pin.Name + " = " + pin.Expression + ")")
            .Append("{")
            .Concat(statements.Select(static statement => Indent(statement, 4)))
            .Append("}");
        return JoinLines(lines);
    }

    private static TedToolkit.RoslynHelper.Generators.Syntaxes.Attribute LayoutAttribute(int size, int alignment)
    {
        return Attribute<StructLayoutAttribute>()
            .AddArgument(Argument(LayoutKind.Sequential.ToExpression()))
            .AddNamedArgument(nameof(StructLayoutAttribute.Size), size.ToLiteral())
            .AddNamedArgument(nameof(StructLayoutAttribute.Pack), alignment.ToLiteral());
    }

    private static Property AutoProperty(string type, string name)
    {
        return new Property(new DataType(type), name).Public
            .AddAccessor(new Accessor(AccessorType.GET));
    }

    private static string GetContainingType(BindingFiniteOperationDefinition operation)
    {
        return operation switch
        {
            BindingCompositeOperationDefinition value => value.ContainingType,
            BindingOwnedOperationDefinition value => value.ContainingType,
            BindingScalarOperationDefinition value => value.ContainingType,
            BindingTwoPhaseOperationDefinition value => value.ContainingType,
            _ => throw new InvalidOperationException("A buffer owner operation is emitted on its owner type."),
        };
    }

    private static string OwnerPointerName(BindingOwnerParameterDefinition owner)
    {
        return owner.Name + "Pointer";
    }

    private static string BufferPointerName(BindingBufferDefinition buffer)
    {
        return buffer.PointerName ?? ToCamelCase(buffer.Name) + "Pointer";
    }

    private static string BufferPointerName(BindingTwoPhaseBufferDefinition buffer)
    {
        return buffer.PointerName ?? ToCamelCase(buffer.PropertyName) + "Pointer";
    }

    private static string ValueFunctionPointerType(BindingValueParameterDefinition value)
    {
        return (value.ManagedTransportType ?? value.ManagedType) + (value.PassByPointer ? "*" : "");
    }

    private static string ValueArgument(BindingValueParameterDefinition value)
    {
        var argument = value.ManagedArgumentExpression.Replace("$value", value.Name, StringComparison.Ordinal);
        return value.PassByPointer ? "&" + argument : argument;
    }

    private static T Find<T>(IReadOnlyList<T> values, string name, Func<T, string> selector)
    {
        return values.Single(value => selector(value) == name);
    }

    private static string ToCamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
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
}