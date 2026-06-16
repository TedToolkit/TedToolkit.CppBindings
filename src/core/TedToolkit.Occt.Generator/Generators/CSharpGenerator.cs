
using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

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

public sealed class CSharpGenerator(
    CXXRecordDecl recordDecl,
    IRecordService recordService,
    IOptions<GenerationOptions> generationOptions,
    ITypeService typeService,
    IFieldService fieldService) : IGenerator
{
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var type = recordService.GetType(recordDecl);
        var cSharpName = typeService.GetCSharpName(type);

        var structName = recordDecl.Bases.Count > 0 ? ZString.Concat(cSharpName, "Data") : cSharpName;
        var structDeclaration = Struct(structName).Unsafe
            .AddAttribute(Attribute<StructLayoutAttribute>()
                .AddArgument(Argument(LayoutKind.Explicit.ToExpression()))
                .AddNamedArgument(nameof(StructLayoutAttribute.Size),
                    (await recordService.GetSizeAsync(recordDecl).ConfigureAwait(false)).ToLiteral()));

        structDeclaration = generationOptions.Value.IsInternal ? structDeclaration.Internal : structDeclaration.Public;
        await GenerateFields(structDeclaration).ConfigureAwait(false);

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(structDeclaration))
            .ToCode();
    }

    private async Task GenerateFields(TypeDeclaration structDeclaration)
    {
        foreach (var fieldDecl in recordService.GetFields(recordDecl))
        {
            var fieldName = fieldService.GetName(fieldDecl);
            var fieldType = fieldService.GetType(fieldDecl);
            var offset = await fieldService.GetOffsetAsync(fieldDecl).ConfigureAwait(false);

            var field = Field(new DataType(typeService.GetCSharpName(fieldType)), fieldName)
                .AddAttribute(Attribute<FieldOffsetAttribute>()
                    .AddArgument(Argument(offset.ToLiteral())))
                .Public;

            structDeclaration.AddMember(field);
        }
    }
}