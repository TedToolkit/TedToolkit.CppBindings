using System.Security.Cryptography;
using System.Reflection;

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.CppBindings.Generator;

if (args.Length != 1)
{
    throw new ArgumentException("Expected one disposable output directory.");
}

var root = new DirectoryInfo(Path.GetFullPath(args[0]));
if (root.Exists)
{
    root.Delete(recursive: true);
}

root.Create();
var managed = root.CreateSubdirectory("managed");
var native = root.CreateSubdirectory("native");
var options = new GenerationOptions
{
    CSharpFolder = managed,
    CppFolder = native,
    CSharpNamespace = "Benchmark.Generated",
    NativeLibraryBaseName = "benchmark_probe",
};

await VerifyShortReadsAsync();

await RunAsync(options, new ProbeProvider("alpha-v1", includeSecond: true, nested: false));
var managedPaths = new[]
{
    Path.Combine(managed.FullName, "Alpha.g.cs"),
    Path.Combine(managed.FullName, "Beta.g.cs"),
    Path.Combine(managed.FullName, "NativeApi.g.cs"),
};
var nativePaths = new[]
{
    Path.Combine(native.FullName, "Provider.cpp"),
    Path.Combine(native.FullName, "NativeFunctionTable.cpp"),
};
var firstManifest = Manifest(root.FullName);
var anchor = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
SetTimestamps(managedPaths.Concat(nativePaths), anchor);
await File.WriteAllTextAsync(Path.Combine(managed.FullName, "stale.txt"), "stale");
Directory.CreateDirectory(Path.Combine(native.FullName, "stale", "nested"));
await File.WriteAllTextAsync(Path.Combine(native.FullName, "stale", "nested", "stale.cpp"), "stale");

await RunAsync(options, new ProbeProvider("alpha-v1", includeSecond: true, nested: false));
Require(firstManifest.SequenceEqual(Manifest(root.FullName)), "Unchanged generation changed the content manifest.");
Require(managedPaths.Concat(nativePaths).All(path => File.GetLastWriteTimeUtc(path) == anchor), "Unchanged generation rewrote an expected file.");
Require(!File.Exists(Path.Combine(managed.FullName, "stale.txt")), "A stale file survived reconciliation.");
Require(!Directory.Exists(Path.Combine(native.FullName, "stale")), "A stale directory survived reconciliation.");

await RunAsync(options, new ProbeProvider("alpha-v2", includeSecond: true, nested: false));
Require(await File.ReadAllTextAsync(managedPaths[0]) == "alpha-v2", "The changed source was not published.");
Require(File.GetLastWriteTimeUtc(managedPaths[0]) != anchor, "The changed source retained its old timestamp.");
Require(managedPaths.Skip(1).Concat(nativePaths).All(path => File.GetLastWriteTimeUtc(path) == anchor), "A single-source edit rewrote unrelated output.");

SetTimestamps(managedPaths.Concat(nativePaths), anchor);
await RunAsync(options, new ProbeProvider("alpha-v2", includeSecond: false, nested: false));
Require(!File.Exists(managedPaths[1]), "A removed source survived reconciliation.");
Require(managedPaths.Take(1).Concat(managedPaths.Skip(2)).Concat(nativePaths).All(path => File.GetLastWriteTimeUtc(path) == anchor), "Removing one source rewrote retained output.");

File.Delete(managedPaths[0]);
await RunAsync(options, new ProbeProvider("alpha-v2", includeSecond: false, nested: false));
Require(await File.ReadAllTextAsync(managedPaths[0]) == "alpha-v2", "A missing output was not restored.");
Require(managedPaths.Skip(2).Concat(nativePaths).All(path => File.GetLastWriteTimeUtc(path) == anchor), "Restoring one missing source rewrote unrelated output.");

var stableHash = Hash(managedPaths[0]);
try
{
    await RunAsync(options, new ProbeProvider("incomplete", includeSecond: false, nested: false, failAlpha: true));
    throw new InvalidOperationException("A failing renderer unexpectedly succeeded.");
}
catch (Exception exception) when (exception.ToString().Contains("probe render failure", StringComparison.Ordinal))
{
}

Require(Hash(managedPaths[0]) == stableHash, "A failing renderer replaced the last complete output.");
Require(!Directory.EnumerateFiles(root.FullName, "*.tmp", SearchOption.AllDirectories).Any(), "A failed render left a staging file.");

await RunAsync(options, new ProbeProvider("nested", includeSecond: false, nested: true));
Require(File.Exists(Path.Combine(managed.FullName, "Shape", "Nested.g.cs")), "Nested output was not published.");
await RunAsync(options, new ProbeProvider("flat", includeSecond: false, nested: false, alphaPath: "Shape"));
Require(File.Exists(Path.Combine(managed.FullName, "Shape")), "A directory-to-file transition failed.");
await RunAsync(options, new ProbeProvider("nested-again", includeSecond: false, nested: true));
Require(File.Exists(Path.Combine(managed.FullName, "Shape", "Nested.g.cs")), "A file-to-directory transition failed.");

static async Task RunAsync(GenerationOptions options, IGenerationProvider provider)
{
    var builder = Pipeline.CreateBuilder();
    builder.Options.PrintLogo = false;
    builder.Options.PrintResults = false;
    builder.Options.ShowProgressInConsole = false;
    builder.Options.DefaultRetryCount = 0;
    builder.Options.ThrowOnPipelineFailure = true;
    var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync();
    var summary = await pipeline.RunAsync();
    Require(summary.Status == Status.Successful, "Generation pipeline failed.");
}

static string[] Manifest(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
    .Where(path => !path.EndsWith("stale.txt", StringComparison.Ordinal))
    .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/') + ":" + Hash(path))
    .Order(StringComparer.Ordinal)
    .ToArray();

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

static async Task VerifyShortReadsAsync()
{
    var method = typeof(GenerationPlan).Assembly
        .GetType("TedToolkit.CppBindings.Generator.GenerationOutput", throwOnError: true)!
        .GetMethod("StreamsEqualAsync", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The stream comparison helper was not found.");
    var bytes = Enumerable.Range(0, 97).Select(value => (byte)value).ToArray();
    await using var first = new ShortReadStream(bytes, 3);
    await using var second = new ShortReadStream(bytes, 5);
    var equalTask = (Task<bool>)method.Invoke(
        null,
        [first, second, new byte[16 * 1024], new byte[16 * 1024], CancellationToken.None])!;
    Require(await equalTask, "Equal streams with different short-read boundaries compared unequal.");

    var changed = bytes.ToArray();
    changed[^2]++;
    await using var third = new ShortReadStream(bytes, 7);
    await using var fourth = new ShortReadStream(changed, 11);
    var unequalTask = (Task<bool>)method.Invoke(
        null,
        [third, fourth, new byte[16 * 1024], new byte[16 * 1024], CancellationToken.None])!;
    Require(!await unequalTask, "Different streams with short reads compared equal.");
}

static void SetTimestamps(IEnumerable<string> paths, DateTime timestamp)
{
    foreach (var path in paths)
    {
        File.SetLastWriteTimeUtc(path, timestamp);
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class ProbeProvider(
    string alpha,
    bool includeSecond,
    bool nested,
    bool failAlpha = false,
    string alphaPath = "Alpha.g.cs") : IGenerationProvider
{
    public IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
    {
        var managed = new List<GeneratedSource>();
        if (nested)
        {
            managed.Add(new GeneratedSource("Shape/Nested.g.cs", (writer, token) => writer.WriteAsync(alpha.AsMemory(), token)));
        }
        else
        {
            managed.Add(new GeneratedSource(alphaPath, RenderAlphaAsync));
        }

        if (includeSecond)
        {
            managed.Add(new GeneratedSource("Beta.g.cs", (writer, token) => writer.WriteAsync("beta".AsMemory(), token)));
        }

        GeneratedSource[] native =
        [
            new("Provider.cpp", (writer, token) => writer.WriteAsync("extern \"C\" void Probe() {}".AsMemory(), token)),
        ];
        return Task.FromResult(new GenerationPlan(managed, native, ["Probe"]));
    }

    private async Task RenderAlphaAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(alpha.AsMemory(), cancellationToken);
        if (failAlpha)
        {
            throw new InvalidOperationException("probe render failure");
        }
    }
}

internal sealed class ShortReadStream(byte[] bytes, int maximumRead) : MemoryStream(bytes)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumRead)], cancellationToken);
    }
}
