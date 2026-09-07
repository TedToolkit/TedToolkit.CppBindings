using System.Text.Json;

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.CppBindings.Cgal.Generator;
using TedToolkit.CppBindings.Generator;

var arguments = ParseArguments(args);
if (!arguments.TryGetValue("--output-root", out var outputRoot)
    || !arguments.TryGetValue("--vcpkg-root", out var vcpkgRoot))
{
    return 1;
}

var options = new CgalGenerationOptions
{
    VcpkgRoot = new DirectoryInfo(vcpkgRoot),
    CSharpFolder = new DirectoryInfo(Path.Combine(outputRoot, "csharp")),
    CppFolder = new DirectoryInfo(Path.Combine(outputRoot, "cpp")),
    CSharpNamespace = "TedToolkit.CppBindings.Cgal",
    NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
    CppVersion = 20,
};
var provider = new CgalGenerationProvider(options);
var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
var builder = Pipeline.CreateBuilder();
builder.Options.PrintLogo = false;
builder.Options.PrintResults = false;
builder.Options.ShowProgressInConsole = false;
builder.Options.DefaultRetryCount = 0;
builder.Options.ThrowOnPipelineFailure = true;
var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync().ConfigureAwait(false);
var summary = await pipeline.RunAsync().ConfigureAwait(false);
if (summary.Status != Status.Successful)
{
    return 2;
}

var result = new
{
    provider.Profile.ProfileId,
    SourceDeclarationCount = provider.Inventory.SourceDeclarations.Count,
    CandidateCount = provider.Inventory.Candidates.Count,
    AdmittedCount = provider.Inventory.Admitted.Count,
    UnsupportedCount = provider.Inventory.Unsupported.Count,
    ManagedArtifactCount = provider.Inventory.ManagedArtifacts.Count,
    NativeArtifactCount = provider.Inventory.NativeArtifacts.Count,
    NativeExportCount = plan.NativeExports.Count,
    provider.Inventory.Toolchain,
};
await File.WriteAllTextAsync(
    Path.Combine(outputRoot, "generation-result.json"),
    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
return 0;

static Dictionary<string, string> ParseArguments(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index + 1 < values.Length; index += 2)
    {
        result[values[index]] = values[index + 1];
    }

    return result;
}
