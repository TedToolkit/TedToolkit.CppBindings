using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator;

public static class PipelineBuilderExtension
{
    public static PipelineBuilder AddOcctGenerators(this PipelineBuilder builder, GenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services
            .AddSingleton<IVcpkgService, VcpkgService>()
            .AddSingleton<IRecordManager, RecordManager>()
            .AddSingleton<ITypeService, TypeService>()
            .AddSingleton<IFieldService, FieldService>()
            .AddSingleton<IRecordService, RecordService>()
            .AddSingleton<IGeneratorService, GeneratorService>()
            .AddModule<ParseModule>()
            .AddModule<RecordModule>()
            .AddModule<GenerateModule>()
            .AddSingleton(
                Microsoft.Extensions.Options.Options.Create(options));

        return builder;
    }
}