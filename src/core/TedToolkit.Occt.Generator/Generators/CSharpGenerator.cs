// -----------------------------------------------------------------------
// <copyright file="CSharpGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using ClangSharp;

using Cysharp.Text;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;
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
/// <param name="recordService">The record metadata service.</param>
/// <param name="recordLayoutService">The native record layout service.</param>
/// <param name="generationOptions">The generator options.</param>
/// <param name="typeService">The type naming service.</param>
/// <param name="fieldService">The field metadata service.</param>
public sealed class CSharpGenerator(
    CXXRecordDecl recordDecl,
    IRecordService recordService,
    IRecordLayoutService recordLayoutService,
    IOptions<GenerationOptions> generationOptions,
    ITypeService typeService,
    IFieldService fieldService) : IGenerator
{
    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var type = recordService.GetType(recordDecl);
        var cSharpName = typeService.GetCSharpName(type);

        var structName = recordDecl.Bases.Count > 0 ? ZString.Concat(cSharpName, "Data") : cSharpName;
        var structDeclaration = Struct(structName).Unsafe
            .AddAttribute(Attribute<StructLayoutAttribute>()
                .AddArgument(Argument(LayoutKind.Explicit.ToExpression()))
                .AddNamedArgument(nameof(StructLayoutAttribute.Size),
                    recordLayoutService.GetSize(recordDecl).ToLiteral()));

        structDeclaration = generationOptions.Value.IsInternal ? structDeclaration.Internal : structDeclaration.Public;
        await GenerateFieldsAsync(structDeclaration).ConfigureAwait(false);

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(structDeclaration))
            .ToCode();
    }

    private async Task GenerateFieldsAsync(TypeDeclaration structDeclaration)
    {
        foreach (var fieldDecl in recordService.GetFields(recordDecl))
        {
            var fieldName = fieldService.GetName(fieldDecl);
            var fieldType = fieldService.GetType(fieldDecl);
            var offset = recordLayoutService.GetOffset(recordDecl, fieldDecl);

            var field = Field(new DataType(typeService.GetCSharpName(fieldType)), fieldName)
                .AddAttribute(Attribute<FieldOffsetAttribute>()
                    .AddArgument(Argument(offset.ToLiteral())))
                .Public;

            structDeclaration.AddMember(field);
        }
    }
}