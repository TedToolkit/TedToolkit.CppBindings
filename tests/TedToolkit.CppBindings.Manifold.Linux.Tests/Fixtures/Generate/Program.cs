using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Manifold.Generator;
using TedToolkit.FileSystem;

if (args.Length != 2)
{
    throw new ArgumentException("Usage: Generate <Manifold install prefix> <generated output directory>");
}

var provider = ManifoldGenerationProvider.FromInstallation(new(args[0]));
var output = new DirectoryPath(args[1]);
var options = new GenerationOptions
{
    RuntimeIdentifier = "linux-x64",
    CSharpFolder = output / "csharp",
    CppFolder = output / "cpp",
    CSharpNamespace = "TedToolkit.CppBindings.Manifold",
    NativeLibraryBaseName = provider.Profile.NativeLibraryBaseName,
    CppVersion = 20,
};
var builder = Pipeline.CreateBuilder();
builder.Options.ThrowOnPipelineFailure = true;
builder.Options.DefaultRetryCount = 0;
var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync();
var summary = await pipeline.RunAsync();
return summary.Status is Status.Successful ? 0 : 1;
