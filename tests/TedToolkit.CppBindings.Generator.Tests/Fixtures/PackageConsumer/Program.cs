using System.Reflection;
using System.Text.Json;

using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;
using ModularPipelines.Modules;

using TedToolkit.CppBindings.Generator;

namespace IndependentProvider;

internal static class Program
{
    private static readonly string[] Scenarios =
    [
        "valid", "repeat", "path_escape", "absolute_path", "case_collision", "file_directory_collision",
        "reserved_managed", "reserved_native", "duplicate_export", "invalid_export",
        "plan_failure", "plan_cancellation", "module_failure", "render_failure", "render_cancellation",
        "unsupported_rid", "overlapping_roots", "invalid_basename", "namespace_keyword", "device_path", "reserved_export",
        "export_trailing_lf", "duplicate_export_trailing_lf", "reserved_export_trailing_lf", "namespace_trailing_lf",
    ];

    private static async Task Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected an isolated evidence directory.");
        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        var results = new List<object>();
        foreach (var scenario in Scenarios)
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, scenario));
            var options = new GenerationOptions
            {
                CSharpFolder = folder.CreateSubdirectory("managed"),
                CppFolder = folder.CreateSubdirectory("native"),
                CSharpNamespace = "Independent.Generated",
                NativeLibraryBaseName = "neutral_fixture",
            };
            var sentinels = new[]
            {
                Path.Combine(options.CSharpFolder.FullName, "stale.txt"),
                Path.Combine(options.CppFolder.FullName, "stale.txt"),
            };
            foreach (var sentinel in sentinels) await File.WriteAllTextAsync(sentinel, "preserve until validated");
            if (scenario == "unsupported_rid") options = options with { RuntimeIdentifier = "linux-x64" };
            if (scenario == "overlapping_roots") options = options with { CppFolder = options.CSharpFolder };
            if (scenario == "invalid_basename") options = options with { NativeLibraryBaseName = "fixture.dll" };
            if (scenario == "namespace_keyword") options = options with { CSharpNamespace = "Independent.class" };
            if (scenario == "namespace_trailing_lf") options = options with { CSharpNamespace = "Independent.Generated\n" };
            var provider = new Provider(scenario);
            Exception? failure = null;
            try
            {
                var builder = Pipeline.CreateBuilder();
                builder.Options.PrintLogo = false;
                builder.Options.PrintResults = false;
                builder.Options.ShowProgressInConsole = false;
                builder.Options.DefaultRetryCount = 0;
                builder.Options.ThrowOnPipelineFailure = true;
                builder.Services.AddModule<PreparationModule>(_ => new PreparationModule(provider));
                var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync();
                var summary = await pipeline.RunAsync();
                Require(summary.Status == Status.Successful, "Pipeline returned a non-success result.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            var valid = scenario is "valid" or "repeat";
            Require(valid == (failure is null), $"Unexpected result for {scenario}: {failure}");
            if (!valid)
            {
                var expectedFailure = scenario switch
                {
                    "path_escape" or "absolute_path" or "device_path" => "Invalid output-relative source path",
                    "case_collision" or "reserved_managed" or "reserved_native" => "Generated source path collision",
                    "file_directory_collision" => "Generated file/directory collision",
                    "duplicate_export" or "invalid_export" or "reserved_export" or "export_trailing_lf"
                        or "duplicate_export_trailing_lf" or "reserved_export_trailing_lf" => "Invalid or duplicate native export",
                    "plan_failure" => "fixture plan failure",
                    "plan_cancellation" => "fixture plan cancellation",
                    "module_failure" => "fixture preparation failure",
                    "render_failure" => "fixture render failure",
                    "render_cancellation" => "fixture render cancellation",
                    "unsupported_rid" => "Only the proved win-x64",
                    "overlapping_roots" => "distinct and non-overlapping",
                    "namespace_keyword" or "namespace_trailing_lf" => "portable C# identifiers",
                    "invalid_basename" => "basename",
                    _ => throw new InvalidOperationException("Unclassified negative proof."),
                };
                Require(failure!.ToString().Contains(expectedFailure, StringComparison.OrdinalIgnoreCase), $"Wrong failure for {scenario}: {failure}");
                var registrationFailure = scenario is "unsupported_rid" or "overlapping_roots" or "invalid_basename" or "namespace_keyword" or "namespace_trailing_lf";
                Require(provider.PlanCalls == (registrationFailure || scenario == "module_failure" ? 0 : 1), $"Wrong plan count for {scenario}.");
                Require(provider.Prepared != (registrationFailure || scenario == "module_failure"), $"Wrong preparation result for {scenario}.");
            }
            if (valid)
            {
                Require(provider.PlanCalls == 1 && provider.RenderCalls == 2, "Plan/render count mismatch.");
                Require(sentinels.All(path => !File.Exists(path)), "Stale output was not cleaned.");
                var managed = await File.ReadAllTextAsync(Path.Combine(options.CSharpFolder.FullName, "Provider.g.cs"));
                var loader = await File.ReadAllTextAsync(Path.Combine(options.CSharpFolder.FullName, "NativeApi.g.cs"));
                var table = await File.ReadAllTextAsync(Path.Combine(options.CppFolder.FullName, "NativeFunctionTable.cpp"));
                Require(managed.Contains("Zebra=0; Alpha=1;"), "Provider lost shared slot ordering.");
                Require(table.IndexOf("&Zebra", StringComparison.Ordinal) < table.IndexOf("&Alpha", StringComparison.Ordinal), "Core reordered exports.");
                Require(loader.Contains("namespace Independent.Generated;") && loader.Contains("neutral_fixture"), "Shared configuration was ignored.");
                Require(!loader.Contains("Occt") && !table.Contains("Occt"), "Core emitted provider knowledge.");
                Require(loader.Contains("NativeLibrary.Load") && loader.Contains("GetFunction(int index)"), "Loader semantics changed.");
                Require(!loader.Contains("fingerprint") && !loader.Contains("manifest") && !loader.Contains("Dispose"), "Unexpected loading protocol.");
            }
            else if (scenario is "render_failure" or "render_cancellation")
            {
                Require(provider.PlanCalls == 1 && provider.RenderCalls > 0, "Renderer failure was not exercised.");
            }
            else
            {
                Require(provider.RenderCalls == 0, "Rendering started before all preparation and validation passed.");
                Require(sentinels.All(File.Exists), "Failed preparation or validation cleared existing outputs.");
                if (scenario is "plan_failure" or "plan_cancellation") Require(provider.PlanCalls == 1, "Plan failure was not exercised.");
                if (scenario == "module_failure") Require(provider.PlanCalls == 0, "Plan started after failed preparation.");
            }
            results.Add(new { scenario, passed = true, provider.PlanCalls, provider.RenderCalls, failure = failure?.ToString() });
        }

        var first = Manifest(Path.Combine(root, "valid"));
        var second = Manifest(Path.Combine(root, "repeat"));
        Require(first.SequenceEqual(second), "Identical input produced different relative-path/content manifests.");
        var assembly = typeof(GenerationPlan).Assembly;
        string[] expectedTypes = ["GenerationOptions", "IGenerationProvider", "GenerationPlan", "GeneratedSource", "CppBindingsPipelineBuilderExtensions", "CleanGenerationOutputModule", "GenerateCSharpModule", "GenerateCppModule"];
        Require(assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal)
            .SequenceEqual(expectedTypes.Select(name => "TedToolkit.CppBindings.Generator." + name).Order(StringComparer.Ordinal)), "Generic public surface drifted.");
        Require(!assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
            .Any(attribute => attribute.AssemblyName == typeof(Program).Assembly.GetName().Name), "Consumer received friend access.");
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(item => item.GetName().Name?.Contains("Occt", StringComparison.OrdinalIgnoreCase) == true), "Consumer loaded OCCT.");
        await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { passed = true, count = results.Count, results, manifest = first }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string[] Manifest(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/') + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))
        .Order(StringComparer.Ordinal).ToArray();

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public sealed class PreparationModule(Provider provider) : Module<bool>
{
    protected override Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (provider.Scenario == "module_failure") throw new InvalidOperationException("fixture preparation failure");
        provider.Prepared = true;
        return Task.FromResult(true);
    }
}

public sealed class Provider(string scenario) : IGenerationProvider
{
    public string Scenario { get; } = scenario;
    public bool Prepared { get; set; }
    public int PlanCalls;
    public int RenderCalls;
    public IReadOnlyList<Type> PreparationModules { get; } = [typeof(PreparationModule)];

    public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref PlanCalls);
        Program.Require(Prepared, "Plan started before provider preparation completed.");
        cancellationToken.ThrowIfCancellationRequested();
        if (Scenario == "plan_failure") throw new InvalidOperationException("fixture plan failure");
        if (Scenario == "plan_cancellation") throw new OperationCanceledException("fixture plan cancellation", new CancellationToken(true));
        var path = Scenario switch
        {
            "path_escape" => "../escape.g.cs",
            "absolute_path" => Path.Combine(Path.GetTempPath(), "escape.g.cs"),
            "reserved_managed" => "NativeApi.g.cs",
            "device_path" => "NUL.g.cs",
            _ => "Provider.g.cs",
        };
        var managed = new List<GeneratedSource> { new(path, (writer, token) => RenderAsync(writer, "// Zebra=0; Alpha=1;", token)) };
        if (Scenario == "case_collision") managed.Add(new("provider.g.cs", (writer, token) => RenderAsync(writer, "", token)));
        if (Scenario == "file_directory_collision") managed.Add(new("Provider.g.cs/Child.g.cs", (writer, token) => RenderAsync(writer, "", token)));
        string[] exports = Scenario switch
        {
            "duplicate_export" => ["Zebra", "Zebra"],
            "invalid_export" => ["invalid();"],
            "reserved_export" => ["NativeApi_GetFunctionTable"],
            "export_trailing_lf" => ["Zebra\n"],
            "duplicate_export_trailing_lf" => ["Zebra", "Zebra\n"],
            "reserved_export_trailing_lf" => ["NativeApi_GetFunctionTable\n"],
            _ => ["Zebra", "Alpha"],
        };
        var native = new[] { new GeneratedSource(Scenario == "reserved_native" ? "NativeFunctionTable.cpp" : "Provider.cpp", (writer, token) => RenderAsync(writer, "extern \"C\" void Zebra() {}\nextern \"C\" void Alpha() {}", token)) };
        var plan = new GenerationPlan(managed, native, exports);
        managed.Clear();
        exports[0] = "MutatedAfterSnapshot";
        return Task.FromResult(plan);
    }

    private async Task RenderAsync(TextWriter writer, string contents, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref RenderCalls);
        Program.Require(Prepared && PlanCalls == 1, "Renderer observed an incomplete or repeated plan.");
        if (Scenario == "render_failure") throw new InvalidOperationException("fixture render failure");
        if (Scenario == "render_cancellation") throw new OperationCanceledException("fixture render cancellation", new CancellationToken(true));
        await writer.WriteAsync(contents.AsMemory(), cancellationToken);
    }
}
