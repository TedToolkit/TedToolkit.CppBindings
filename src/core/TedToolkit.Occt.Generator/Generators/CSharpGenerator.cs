// -----------------------------------------------------------------------
// <copyright file="CSharpGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models;
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
public sealed class CSharpGenerator(
    RecordModel recordDecl,
    IOptions<GenerationOptions> generationOptions) : IGenerator
{
    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var structName = recordDecl.Type.CSharpPublicType.ToCode();
        var structDeclaration = Struct(structName).Unsafe
            .AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                .AddArgument(Argument(recordDecl.Type.CppTypeName.ToLiteral())))
            .AddAttribute(Attribute<StructLayoutAttribute>()
                .AddArgument(Argument(LayoutKind.Explicit.ToExpression()))
                .AddNamedArgument(nameof(StructLayoutAttribute.Size),
                    recordDecl.Size.ToLiteral()));
        AddRootDescriptions(structDeclaration, recordDecl.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        structDeclaration = generationOptions.Value.IsInternal ? structDeclaration.Internal : structDeclaration.Public;
        GenerateFields(structDeclaration);
        GenerateMethods(structDeclaration);

        var nameSpace = NameSpace("TedToolkit.Occt")
            .AddMember(structDeclaration);

        GenerateInterfaces(nameSpace, structDeclaration);

        return File()
            .AddNameSpace(nameSpace)
            .ToCode();
    }

    private void GenerateInterfaces(NameSpace nameSpace, TypeDeclaration structDeclaration)
    {
        if (recordDecl.Base is null)
        {
            return;
        }

        var interfaceDeclaration = Interface(recordDecl.Type.CSharpInterfaceName).Public.Unsafe;
        structDeclaration.AddBaseType(new DataType(recordDecl.Type.CSharpInterfaceName));
        interfaceDeclaration.AddBaseType(new DataType(recordDecl.Base.Type.CSharpInterfaceName));

        nameSpace.AddMember(interfaceDeclaration);
    }

    private void GenerateFields(TypeDeclaration structDeclaration)
    {
        foreach (var fieldDecl in recordDecl.FieldModels)
        {
            AddField(structDeclaration, fieldDecl);
        }
    }

    private void GenerateMethods(TypeDeclaration structDeclaration)
    {
        GenerateOneStructMethods(structDeclaration, recordDecl);
    }

    private static void GenerateOneStructMethods(TypeDeclaration structDeclaration, RecordModel recordModel)
    {
        if (recordModel.Base is not null)
        {
            GenerateOneStructMethods(structDeclaration, recordModel.Base);
        }

        foreach (var recordDeclMethodModel in recordModel.MethodModels)
        {
            if (recordDeclMethodModel.Type is not MethodModelType.Normal)
            {
                continue;
            }

            AddMethod(structDeclaration, recordDeclMethodModel,
                recordDeclMethodModel.GetMethodInteropName(recordModel));
        }
    }

    private static void AddField(TypeDeclaration structDeclaration, FieldModel fieldModel)
    {
        var field = Field(fieldModel.Type.CSharpPInvokeType, fieldModel.Name)
            .AddAttribute(Attribute<FieldOffsetAttribute>()
                .AddArgument(Argument(fieldModel.Offset.ToLiteral())))
            .AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                .AddArgument(Argument(fieldModel.Type.CppTypeName.ToLiteral())))
            .Public;
        AddRootDescriptions(field, fieldModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        structDeclaration.AddMember(field);
    }

    private static void AddMethod(TypeDeclaration structDeclaration, MethodModel methodModel, string pinvokeMethodName)
    {
        var method = Method(methodModel.MethodName, CreateReturnType(methodModel)).Public;
        AddRootDescriptions(method, methodModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        if (!methodModel.IsReturnVoid)
        {
            method.AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                .AddArgument(Argument(methodModel.ReturnType.CppTypeName.ToLiteral())));
        }

        if (methodModel.IsConst)
        {
            method = method.Readonly;
        }

        if (!methodModel.NoExceptions)
        {
            method.AddRootDescription(
                new DescriptionInheritDoc(new DataType("global::TedToolkit.Occt.interop_error.ThrowIfError")));
        }

        foreach (var parameterModel in methodModel.Parameters)
        {
            var parameter = Parameter(parameterModel.Type.CSharpPublicType, parameterModel.Name)
                .AddAttribute(Attribute(new DataType("global::TedToolkit.Occt.Attributes.NativeTypeNameAttribute"))
                    .AddArgument(Argument(parameterModel.Type.CppTypeName.ToLiteral())));
            AddDescriptions(parameter, parameterModel.DescriptionItems, static (target, description) =>
                target.AddDescription(description));
            method.AddParameter(parameter);
        }

        structDeclaration.AddMember(method);
    }

    private static ReturnType CreateReturnType(MethodModel methodModel)
    {
        var returnType = new ReturnType(methodModel.ReturnType.CSharpPublicType);
        AddDescriptions(returnType, methodModel.ReturnTypeDescriptionItems, static (target, description) =>
            target.AddDescription(description));
        return returnType;
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

    private static void AddDescriptions<TTarget>(
        TTarget target,
        IReadOnlyList<IDescriptionItem> descriptions,
        Action<TTarget, IDescriptionItem> addDescription)
    {
        foreach (var description in descriptions)
        {
            addDescription(target, description);
        }
    }
}