using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.CppBindings.Cgal.Generator;
using TedToolkit.CppBindings.Generator;

if (args.Length != 2)
{
    return 1;
}

var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
if (string.IsNullOrWhiteSpace(vcpkgRoot))
{
    vcpkgRoot = @"C:\vcpkg";
}

var managedRoot = new DirectoryInfo(Path.Combine(args[1], "csharp"));
var nativeRoot = new DirectoryInfo(Path.Combine(args[1], "cpp"));
var options = new CgalGenerationOptions
{
    VcpkgRoot = new(vcpkgRoot),
    CSharpFolder = managedRoot,
    CppFolder = nativeRoot,
    CSharpNamespace = "TedToolkit.CppBindings.Cgal",
    NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
    CppVersion = 20,
};
var provider = new CgalGenerationProvider(options);
var plan = await provider.CreatePlanAsync(CancellationToken.None);
var builder = Pipeline.CreateBuilder();
builder.Options.PrintLogo = false;
builder.Options.PrintResults = false;
builder.Options.ShowProgressInConsole = false;
builder.Options.DefaultRetryCount = 0;
builder.Options.ThrowOnPipelineFailure = true;
var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync();
var summary = await pipeline.RunAsync();
if (summary.Status != Status.Successful)
{
    return 2;
}

var managed = ReadSources(managedRoot);
var native = ReadSources(nativeRoot);
var paired = provider.Inventory.ManagedArtifacts.All(item => managed.ContainsKey(item.RelativePath))
    && provider.Inventory.NativeArtifacts.All(item => native.ContainsKey(item.RelativePath));
if (!paired || !managed.ContainsKey("NativeApi.g.cs") || !native.ContainsKey("NativeFunctionTable.cpp")
    || provider.Inventory.Candidates.Count
    != provider.Inventory.Admitted.Count + provider.Inventory.Unsupported.Count
    || provider.Inventory.Admitted.Count != provider.Profile.Declarations.Count
    || provider.Inventory.Unsupported.Count == 0
    || !provider.Inventory.Unsupported.Any(static item =>
        item.NativeSignature.Contains("Point_2::dimension", StringComparison.Ordinal))
    || plan.NativeExports.Count != 10)
{
    return 3;
}

var result = new
{
    provider.Profile.ProfileId,
    DeclarationCount = provider.Inventory.Candidates.Count,
    AdmittedCount = provider.Inventory.Admitted.Count,
    UnsupportedCount = provider.Inventory.Unsupported.Count,
    HeaderCount = provider.Inventory.Sources.Count,
    ExportCount = plan.NativeExports.Count,
    provider.Inventory.Toolchain,
    ManagedHash = Hash(managed),
    NativeHash = Hash(native),
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(result));
return 0;

static SortedDictionary<string, string> ReadSources(DirectoryInfo root)
{
    var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
    {
        result.Add(Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/'), File.ReadAllText(file.FullName));
    }

    return result;
}

static string Hash(IReadOnlyDictionary<string, string> sources)
{
    var canonical = string.Join("\n", sources.Select(static item => item.Key + "\n" + item.Value));
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
