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

        structDeclaration = generationOptions.Value.IsInternal ? structDeclaration.Internal : structDeclaration.Public;
        await GenerateFieldsAsync(structDeclaration).ConfigureAwait(false);

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(structDeclaration))
            .ToCode();
    }

    private async Task GenerateFieldsAsync(TypeDeclaration structDeclaration)
    {
        foreach (var fieldDecl in recordDecl.FieldModels)
        {
            var field = Field(fieldDecl.Type.CSharpPInvokeType, fieldDecl.Name)
                .AddAttribute(Attribute<FieldOffsetAttribute>()
                    .AddArgument(Argument(fieldDecl.Offset.ToLiteral())))
                .Public;

            structDeclaration.AddMember(field);
        }
    }
}