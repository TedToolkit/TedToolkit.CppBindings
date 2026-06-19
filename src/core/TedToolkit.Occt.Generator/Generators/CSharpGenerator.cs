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
            .AddAttribute(Attribute<StructLayoutAttribute>()
                .AddArgument(Argument(LayoutKind.Explicit.ToExpression()))
                .AddNamedArgument(nameof(StructLayoutAttribute.Size),
                    recordDecl.Size.ToLiteral()));
        AddRootDescriptions(structDeclaration, recordDecl.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        structDeclaration = generationOptions.Value.IsInternal ? structDeclaration.Internal : structDeclaration.Public;
        GenerateFields(structDeclaration);
        GenerateMethods(structDeclaration);

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(structDeclaration))
            .ToCode();
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
        foreach (var recordDeclMethodModel in recordDecl.MethodModels)
        {
            AddMethod(structDeclaration, recordDeclMethodModel);
        }
    }

    private static void AddField(TypeDeclaration structDeclaration, FieldModel fieldModel)
    {
        var field = Field(fieldModel.Type.CSharpPInvokeType, fieldModel.Name)
            .AddAttribute(Attribute<FieldOffsetAttribute>()
                .AddArgument(Argument(fieldModel.Offset.ToLiteral())))
            .Public;
        AddRootDescriptions(field, fieldModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));
        structDeclaration.AddMember(field);
    }

    private static void AddMethod(TypeDeclaration structDeclaration, MethodModel methodModel)
    {
        var method = Method(methodModel.MethodName, CreateReturnType(methodModel)).Public;
        AddRootDescriptions(method, methodModel.DescriptionItems, static (target, description) =>
            target.AddRootDescription(description));

        foreach (var parameterModel in methodModel.Parameters)
        {
            var parameter = Parameter(parameterModel.Type.CSharpPublicType, parameterModel.Name);
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
