using System.Text;

using ModularPipelines;

using Sourcy.DotNet;

using TedToolkit.Occt.Generator;
using TedToolkit.Occt.Generator.Options;

Console.OutputEncoding = Encoding.UTF8;

var outputFolder = Solutions.TedToolkit_Occt.Directory
                       ?.CreateSubdirectory("output")
                       .CreateSubdirectory("generated")
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
            CommandLineArgs = [],
        }).BuildAsync().ConfigureAwait(false);
await pipeline
    .RunAsync().ConfigureAwait(false);
