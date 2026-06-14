using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class GeneratorService(
    IRecordService recordService,
    IOptions<GenerationOptions> generationOptions,
    ITypeService typeService,
    IFieldService fieldService) : IGeneratorService
{
    public CSharpGenerator GenerateCSharp(CXXRecordDecl record)
    {
        return new CSharpGenerator(record, recordService, generationOptions, typeService, fieldService);
    }

    public CppGenerator GenerateCpp(CXXRecordDecl record)
    {
        return new CppGenerator();
    }
}