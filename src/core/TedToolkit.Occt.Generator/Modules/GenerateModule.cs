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
    ITypeService typeService) :
    Module<bool>
{
    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

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
    }
    private async Task GenerateCSharp(CXXRecordDecl record, CancellationToken cancellationToken)
    {
        var csharpFile = Path.Combine(generationOptions.Value.CSharpFolder.FullName,
            ZString.Concat(typeService.GetCSharpName(record.TypeForDecl), ".g.cs"));
    }
}