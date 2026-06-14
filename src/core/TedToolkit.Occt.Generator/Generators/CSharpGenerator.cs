
using System.Runtime.InteropServices;

using ClangSharp;

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
    public async Task<string> GenerateAsync()
    {
        var type = recordService.GetType(recordDecl);
        var cSharpName = typeService.GetCSharpName(type);
        var typeDeclaration = recordDecl.Bases.Count > 0 ? Class(cSharpName).Sealed : Struct(cSharpName);

        typeDeclaration = typeDeclaration.Unsafe;
        typeDeclaration = generationOptions.Value.IsInternal ? typeDeclaration.Internal : typeDeclaration.Public;

        foreach (var fieldDecl in recordService.GetFields(recordDecl))
        {
            var fieldName = fieldService.GetName(fieldDecl);
            var fieldType = fieldService.GetType(fieldDecl);
            var offset = await fieldService.GetOffsetAsync(fieldDecl).ConfigureAwait(false);

            var field = Field(new DataType(typeService.GetCSharpName(fieldType)), fieldName)
                .AddAttribute(Attribute<FieldOffsetAttribute>()
                    .AddArgument(Argument(offset.ToLiteral())))
                .Private;

            typeDeclaration.AddMember(field);
        }

        return File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(typeDeclaration))
            .ToString()
            ?? throw new InvalidOperationException("Could not generate C# record");
    }
}