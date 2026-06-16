using System.Runtime.InteropServices;

using ClangSharp;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Generated;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

[DependsOn<ParseModule>]
public sealed class RecordModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordManager recordManager,
    IRecordService recordService,
    ITypeService typeService) :
    Module<TranslationUnit>
{
    protected override async Task<TranslationUnit?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var parseModule = await context.GetParseModule();

        var translationUnit = parseModule.ValueOrDefault
                                    ?? throw new InvalidOperationException("TranslationUnit is null");

        var names = generationOptions.Value.DeclOptions.Select(i => i.FileName).ToArray();

        foreach (var cxxRecordDecl in translationUnit.TranslationUnitDecl.CursorChildren
                     .OfType<CXXRecordDecl>()
                     .Where(r => names.Contains(r.Name)))
        {
            AddRecordDecl(cxxRecordDecl);
        }

        return translationUnit;
    }

    private void AddRecordDecl(CXXRecordDecl decl)
    {
        decl = decl.Definition ?? decl;

        if (!recordManager.Add(decl))
        {
            return;
        }

        foreach (var fieldDecl in recordService.GetFields(decl))
        {
            AddType(fieldDecl.Type);
        }

        foreach (var methodDecl in recordService.GetMethods(decl))
        {
            AddType(methodDecl.ReturnType);
            foreach (var methodDeclParameter in methodDecl.Parameters)
            {
                AddType(methodDeclParameter.Type);
            }
        }
    }

    private void AddType(ClangSharp.Type type)
    {
        type = typeService.DesugarType(type);
        if (type.AsCXXRecordDecl is { } decl)
        {
            AddRecordDecl(decl);
        }
    }
}