using ClangSharp;

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Generated;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

[DependsOn<RecordModule>]
public sealed class GenerateModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordManager recordManager,
    ITypeService typeService,
    IGeneratorService generatorService) :
    Module<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var parseModule = await context.GetRecordModule();

        using var translationUnit = parseModule.ValueOrDefault
                                    ?? throw new InvalidOperationException("TranslationUnit is null");
        var tasks = new List<Task>();
        while (recordManager.TryPop(out var record))
        {
            tasks.Add(context.SubModule(
                record.Name,
                () => Task.WhenAll(
                    GenerateCpp(record, cancellationToken),
                    GenerateCSharp(record, cancellationToken))));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return true;
    }

    private async Task GenerateCpp(CXXRecordDecl record, CancellationToken cancellationToken)
    {
        var cppFile = Path.Combine(generationOptions.Value.CppFolder.FullName,
            ZString.Concat(typeService.GetCSharpName(record.TypeForDecl), ".cpp"));

        var codes = await generatorService.GenerateCpp(record).GenerateAsync(cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(cppFile, codes, cancellationToken).ConfigureAwait(false);
    }

    private async Task GenerateCSharp(CXXRecordDecl record, CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(typeService.GetCSharpName(record.TypeForDecl), ".g.cs"));

        var codes = await generatorService.GenerateCSharp(record).GenerateAsync(cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(csharpFile, codes, cancellationToken).ConfigureAwait(false);
    }
}