using System.Security.Cryptography;
using System.Text.Json;
using ModularPipelines;
using ModularPipelines.Enums;
using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Occt.Generator;

if (args.Length != 1) throw new ArgumentException("Expected an isolated output root.");
var root = Directory.CreateDirectory(Path.GetFullPath(args[0]));
var selector = new OcctDeclarationOptions(OcctHeaderType.gp_Pnt2d);
if (selector.FileName != "gp_Pnt2d") throw new InvalidOperationException("Header source generator was not discovered.");
string[] expectedTypes = ["OcctGenerationOptions", "OcctDeclarationOptions", "OcctPipelineBuilderExtensions", "OcctParseModule", "OcctCompilerProbeModule"];
var types = typeof(OcctGenerationOptions).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal).ToArray();
if (!types.SequenceEqual(expectedTypes.Select(name => "TedToolkit.CppBindings.Occt.Generator." + name).Order(StringComparer.Ordinal)))
    throw new InvalidOperationException("Unexpected provider public API: " + string.Join(", ", types));
foreach (var run in new[] { "first", "second" })
{
    var folder = root.CreateSubdirectory(run);
    var builder = Pipeline.CreateBuilder();
    builder.Options.PrintLogo = false;
    builder.Options.PrintResults = false;
    builder.Options.ShowProgressInConsole = false;
    builder.Options.DefaultRetryCount = 0;
    builder.Options.ThrowOnPipelineFailure = true;
    var options = new OcctGenerationOptions
    {
        DeclOptions = [selector],
        CSharpFolder = folder.CreateSubdirectory("managed"),
        CppFolder = folder.CreateSubdirectory("native"),
        CSharpNamespace = "Independent.OcctBindings",
        NativeLibraryBaseName = "occt_package_fixture",
    };
    var pipeline = await builder.AddOcctGenerators(options).BuildAsync();
    var summary = await pipeline.RunAsync();
    if (summary.Status != Status.Successful) throw new InvalidOperationException("Packaged OCCT generation failed.");
    var point = await File.ReadAllTextAsync(Path.Combine(options.CSharpFolder.FullName, "gp_Pnt2d.g.cs"));
    var loader = await File.ReadAllTextAsync(Path.Combine(options.CSharpFolder.FullName, "NativeApi.g.cs"));
    var table = await File.ReadAllTextAsync(Path.Combine(options.CppFolder.FullName, "NativeFunctionTable.cpp"));
    if (!point.Contains("namespace Independent.OcctBindings") || point.Contains("namespace TedToolkit.CppBindings.Occt")
        || !point.Contains("global::Independent.OcctBindings.NativeApi.GetFunction(") || !loader.Contains("occt_package_fixture") || !table.Contains("gp_Pnt2d_Create"))
        throw new InvalidOperationException("Packaged provider did not use the shared configuration and export pipeline.");
}
var first = Manifest(Path.Combine(root.FullName, "first"));
var second = Manifest(Path.Combine(root.FullName, "second"));
if (first.Length == 0 || !first.SequenceEqual(second)) throw new InvalidOperationException("Repeated OCCT generation is not deterministic.");
await File.WriteAllTextAsync(Path.Combine(root.FullName, "result.json"), JsonSerializer.Serialize(
    new { passed = true, runs = 2, files = first.Length, publicTypes = types, manifest = first }, new JsonSerializerOptions { WriteIndented = true }));

static string[] Manifest(string folder) => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
    .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/') + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))
    .Order(StringComparer.Ordinal).ToArray();
