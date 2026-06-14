using ModularPipelines;

using Sourcy.DotNet;

using TedToolkit.Occt.Generator;
using TedToolkit.Occt.Generator.Options;

var outputFolder = Solutions.TedToolkit_Occt.Directory?.CreateSubdirectory("output")
    ?? throw new InvalidOperationException("Output folder not found");

var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(
        new GenerationOptions
        {
            DeclOptions =
            [
                new(OcctHeaderType.Geom2d_BSplineCurve)
            ],
            CSharpFolder = outputFolder.CreateSubdirectory("csharp"),
            CppFolder = outputFolder.CreateSubdirectory("cpp"),
        }).BuildAsync().ConfigureAwait(false);
await pipeline
    .RunAsync().ConfigureAwait(false);
