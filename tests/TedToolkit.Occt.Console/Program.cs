
using ClangSharp;
using ClangSharp.Interop;

using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.Occt.Generator;
using TedToolkit.Occt.Generator.Options;

var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(
        new GenerationOptions(
            false,[
                new(OcctHeaderType.Geom2d_BSplineCurve),
            ]))
    .BuildAsync().ConfigureAwait(false);
await pipeline
    .RunAsync().ConfigureAwait(false);