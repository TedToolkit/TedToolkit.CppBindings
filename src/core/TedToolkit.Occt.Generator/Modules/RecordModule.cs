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
    IRecordService recordService):
    Module<bool>
{
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var parseModule = await context.GetParseModule();

        using var translationUnit = parseModule.ValueOrDefault
                                    ?? throw new InvalidOperationException("TranslationUnit is null");

        var names = generationOptions.Value.DeclOptions.Select(i => i.FileName).ToArray();

        foreach (var cxxRecordDecl in translationUnit.TranslationUnitDecl.CursorChildren
                     .OfType<CXXRecordDecl>()
                     .Where(r => names.Contains(r.Name)))
        {
            recordManager.Add(cxxRecordDecl);
            foreach (var fieldDecl in recordService.GetFields(cxxRecordDecl))
            {
                recordManager.Add(fieldDecl.Type);
            }

            foreach (var methodDecl in recordService.GetMethods(cxxRecordDecl))
            {
                recordManager.Add(methodDecl.ReturnType);
                foreach (var methodDeclParameter in methodDecl.Parameters)
                {
                    recordManager.Add(methodDeclParameter.Type);
                }
            }
        }

        return true;
    }
}