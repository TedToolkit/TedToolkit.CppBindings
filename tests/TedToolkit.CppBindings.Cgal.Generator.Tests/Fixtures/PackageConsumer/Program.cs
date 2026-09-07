using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

var provider = new CgalGenerationProvider(new()
{
    VcpkgRoot = new(vcpkgRoot),
    CSharpFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-package-managed")),
    CppFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-package-native")),
    CSharpNamespace = "TedToolkit.CppBindings.Cgal",
    NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
    CppVersion = 20,
});
var plan = await provider.CreatePlanAsync(CancellationToken.None);
var managed = await RenderAsync(plan.CSharpSources);
var native = await RenderAsync(plan.CppSources);
var paired = provider.Inventory.ManagedArtifacts.All(item => managed.ContainsKey(item.RelativePath))
    && provider.Inventory.NativeArtifacts.All(item => native.ContainsKey(item.RelativePath));
if (!paired || provider.Inventory.Candidates.Count != provider.Inventory.Admitted.Count
    || provider.Inventory.Unsupported.Count != 0 || plan.NativeExports.Count != 12)
{
    return 2;
}

await WriteAsync(managed, Path.Combine(args[1], "csharp"));
await WriteAsync(native, Path.Combine(args[1], "cpp"));
var result = new
{
    provider.Profile.ProfileId,
    DeclarationCount = provider.Inventory.Candidates.Count,
    HeaderCount = provider.Inventory.Sources.Count,
    ExportCount = plan.NativeExports.Count,
    provider.Inventory.Toolchain,
    ManagedHash = Hash(managed),
    NativeHash = Hash(native),
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(result));
return 0;

static async Task<SortedDictionary<string, string>> RenderAsync(IEnumerable<GeneratedSource> sources)
{
    var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var source in sources)
    {
        var writer = new StringWriter();
        await using (writer.ConfigureAwait(false))
        {
            await source.RenderAsync(writer, CancellationToken.None);
            result.Add(source.RelativePath, writer.ToString());
        }
    }

    return result;
}

static string Hash(IReadOnlyDictionary<string, string> sources)
{
    var canonical = string.Join("\n", sources.Select(static item => item.Key + "\n" + item.Value));
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}

static async Task WriteAsync(IReadOnlyDictionary<string, string> sources, string root)
{
    Directory.CreateDirectory(root);
    foreach (var source in sources)
    {
        var path = Path.Combine(root, source.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, source.Value);
    }
}
