using System.Reflection;
using System.Text.Json;

using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;
using ModularPipelines.Modules;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

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
                Require(managed.Contains("NativeApi.GetFunction(0)") && managed.Contains("NativeApi.GetFunction(1)"),
                    "Shared did not emit methods from its deterministic slot ordering.");
                Require(managed.Contains("public unsafe struct FixtureBox<") && managed.Contains("where T: unmanaged"),
                    "Shared did not emit the normalized template representation.");
                Require(managed.Contains("public int Value;"), "Shared did not emit normalized field layout.");
                var native = await File.ReadAllTextAsync(Path.Combine(options.CppFolder.FullName, "Provider.cpp"));
                Require(native.Contains("extern \"C\" int Alpha(") && native.Contains("extern \"C\" int Zebra("),
                    "Shared did not emit matching native method bodies.");
                Require(table.IndexOf("&Alpha", StringComparison.Ordinal) < table.IndexOf("&Zebra", StringComparison.Ordinal), "Shared did not assign deterministic export order.");
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

        await VerifyNestedSemanticSnapshotAsync();

        var first = Manifest(Path.Combine(root, "valid"));
        var second = Manifest(Path.Combine(root, "repeat"));
        Require(first.SequenceEqual(second), "Identical input produced different relative-path/content manifests.");
        var assembly = typeof(GenerationPlan).Assembly;
        string[] expectedTypes =
        [
            "TedToolkit.CppBindings.Generator.GenerationOptions",
            "TedToolkit.CppBindings.Generator.IGenerationProvider",
            "TedToolkit.CppBindings.Generator.GenerationPlan",
            "TedToolkit.CppBindings.Generator.GeneratedSource",
            "TedToolkit.CppBindings.Generator.CppBindingsPipelineBuilderExtensions",
            "TedToolkit.CppBindings.Generator.CleanGenerationOutputModule",
            "TedToolkit.CppBindings.Generator.GenerateCSharpModule",
            "TedToolkit.CppBindings.Generator.GenerateCppModule",
            "TedToolkit.CppBindings.Generator.Semantics.BindingSemanticEngine",
            "TedToolkit.CppBindings.Generator.Semantics.BaseRelationModel",
            "TedToolkit.CppBindings.Generator.Semantics.BindingDeclaration",
            "TedToolkit.CppBindings.Generator.Semantics.BindingEmissionProfile",
            "TedToolkit.CppBindings.Generator.Semantics.BindingEnumEmitter",
            "TedToolkit.CppBindings.Generator.Semantics.BindingNativeProject",
            "TedToolkit.CppBindings.Generator.Semantics.BindingNativeExceptionProjection",
            "TedToolkit.CppBindings.Generator.Semantics.BindingNativeErrorCatchEmitter",
            "TedToolkit.CppBindings.Generator.Semantics.BindingManagedEmitter",
            "TedToolkit.CppBindings.Generator.Semantics.BindingNativeEmitter",
            "TedToolkit.CppBindings.Generator.Semantics.BindingProviderModel",
            "TedToolkit.CppBindings.Generator.Semantics.BindingSemanticDeclaration",
            "TedToolkit.CppBindings.Generator.Semantics.BindingSemanticModel",
            "TedToolkit.CppBindings.Generator.Semantics.BindingSourceDefinition",
            "TedToolkit.CppBindings.Generator.Semantics.CppTypeDescriptor",
            "TedToolkit.CppBindings.Generator.Semantics.BindingTypeProjection",
            "TedToolkit.CppBindings.Generator.Semantics.BindingLayoutDescriptor",
            "TedToolkit.CppBindings.Generator.Semantics.BindingTemplateDescriptor",
            "TedToolkit.CppBindings.Generator.Semantics.BindingAdmission",
            "TedToolkit.CppBindings.Generator.Semantics.EnumMemberModel",
            "TedToolkit.CppBindings.Generator.Semantics.EnumModel",
            "TedToolkit.CppBindings.Generator.Semantics.FieldModel",
            "TedToolkit.CppBindings.Generator.Semantics.IBindingTypeRule",
            "TedToolkit.CppBindings.Generator.Semantics.IBindingLayoutPolicy",
            "TedToolkit.CppBindings.Generator.Semantics.IBindingSourceEmitter",
            "TedToolkit.CppBindings.Generator.Semantics.IBindingTemplatePolicy",
            "TedToolkit.CppBindings.Generator.Semantics.IBindingEmitterPrimitive",
            "TedToolkit.CppBindings.Generator.Semantics.MethodModel",
            "TedToolkit.CppBindings.Generator.Semantics.MethodModelType",
            "TedToolkit.CppBindings.Generator.Semantics.NativeExportInventory",
            "TedToolkit.CppBindings.Generator.Semantics.NativeExportNameBuilder",
            "TedToolkit.CppBindings.Generator.Semantics.NativeObjectKind",
            "TedToolkit.CppBindings.Generator.Semantics.ParameterModel",
            "TedToolkit.CppBindings.Generator.Semantics.PointerAdjustmentKind",
            "TedToolkit.CppBindings.Generator.Semantics.RecordModel",
            "TedToolkit.CppBindings.Generator.Semantics.SemanticGenerationProvider",
            "TedToolkit.CppBindings.Generator.Semantics.TemplateArgumentProjection",
            "TedToolkit.CppBindings.Generator.Semantics.TemplateArgumentProjectionKind",
            "TedToolkit.CppBindings.Generator.Semantics.TemplateProjectionModel",
            "TedToolkit.CppBindings.Generator.Semantics.TypeIndirectionKind",
            "TedToolkit.CppBindings.Generator.Semantics.TypeIndirectionModel",
            "TedToolkit.CppBindings.Generator.Semantics.TypeModel",
            "TedToolkit.CppBindings.Generator.Semantics.TypeTransportModel",
        ];
        var actualTypes = assembly.GetExportedTypes().Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();
        Require(actualTypes.SequenceEqual(expectedTypes.Order(StringComparer.Ordinal)),
            "Generic public surface drifted: " + string.Join(", ", actualTypes));
        Require(!assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
            .Any(attribute => attribute.AssemblyName == typeof(Program).Assembly.GetName().Name), "Consumer received friend access.");
        Require(!AppDomain.CurrentDomain.GetAssemblies().Any(item => item.GetName().Name?.Contains("Occt", StringComparison.OrdinalIgnoreCase) == true), "Consumer loaded OCCT.");
        await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { passed = true, count = results.Count, results, manifest = first }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task VerifyNestedSemanticSnapshotAsync()
    {
        var provider = new Provider("valid");
        var (plan, record) = await provider.CreateSnapshotPlanAsync();

        record.MethodModels[0].NativeExportName = "MutatedAfterPlan";
        record.Alignment = 16;
        record.Type.CSharpPublicType.PointCounter = 2;
        record.FieldModels[0].Type.CSharpPInvokeType.PointCounter = 1;
        provider.MutateDescriptionsAfterPlan();
        record.FieldModels = [];

        var managed = await RenderAsync(plan.CSharpSources.Single(source => source.RelativePath == "Provider.g.cs"));
        var native = await RenderAsync(plan.CppSources.Single(source => source.RelativePath == "Provider.cpp"));
        Require(plan.NativeExports.SequenceEqual(["Alpha", "Zebra"]), "Plan export inventory observed nested mutation.");
        Require(managed.Contains("public int Value;"), "Managed output observed nested field mutation.");
        Require(!managed.Contains("FixtureBox**", StringComparison.Ordinal)
            && !managed.Contains("int*", StringComparison.Ordinal),
            "Managed output retained caller-owned mutable type syntax.");
        Require(managed.Contains("NativeApi.GetFunction(0)") && managed.Contains("NativeApi.GetFunction(1)"),
            "Managed output diverged from the frozen function table.");
        Require(native.Contains("extern \"C\" int Alpha(") && native.Contains("extern \"C\" int Zebra("),
            "Native output observed nested export mutation.");
        Require(!native.Contains("MutatedAfterPlan"), "Native output retained caller-owned semantic state.");
        Require(!managed.Contains("description mutated", StringComparison.Ordinal),
            "Managed output retained caller-owned description syntax.");
        Require(provider.DescriptionsWereSnapshottedOnce,
            "Shared retained a caller-owned record, field, method, return, or parameter description node.");
    }

    private static async Task<string> RenderAsync(GeneratedSource source)
    {
        var writer = new StringWriter();
        await using (writer)
        {
            await source.RenderAsync(writer, CancellationToken.None);
            return writer.ToString();
        }
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

public sealed class Provider : SemanticGenerationProvider
{
    public Provider(string scenario)
        : base(CreateSemanticEngine(scenario))
    {
        Scenario = scenario;
    }

    public string Scenario { get; }
    public bool Prepared { get; set; }
    public int PlanCalls;
    public int RenderCalls;
    public override IReadOnlyList<Type> PreparationModules { get; } = [typeof(PreparationModule)];

    public async Task<(GenerationPlan Plan, RecordModel Record)> CreateSnapshotPlanAsync()
    {
        Prepared = true;
        var model = await CreateProviderModelAsync(CancellationToken.None);
        return (CreateSemanticEngine(Scenario).CreatePlan(model), CurrentRecord!);
    }

    private RecordModel? CurrentRecord { get; set; }

    private MutableDescription[] CurrentDescriptions { get; set; } = [];

    public bool DescriptionsWereSnapshottedOnce => CurrentDescriptions.All(static description => description.Calls == 1);

    public void MutateDescriptionsAfterPlan()
    {
        foreach (var description in CurrentDescriptions)
        {
            description.Text = "description mutated";
        }
    }

    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref PlanCalls);
        Program.Require(Prepared, "Plan started before provider preparation completed.");
        cancellationToken.ThrowIfCancellationRequested();
        if (Scenario == "plan_failure") throw new InvalidOperationException("fixture plan failure");
        if (Scenario == "plan_cancellation") throw new OperationCanceledException("fixture plan cancellation", new CancellationToken(true));
        var managed = new List<BindingSourceDefinition>
        {
            new(Scenario == "case_collision" ? "provider.g.cs" : "fixture-managed.txt",
                (writer, token) => RenderAsync(writer, "fixture managed metadata", token)),
        };
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
        var scalar = new TypeModel
        {
            CppTypeName = "int",
            CppValueTypeName = "int",
            CSharpPInvokeType = new("int"),
            CSharpPublicType = new("int"),
        };
        var recordDescription = new MutableDescription("<summary>Record description original.</summary>");
        var fieldDescription = new MutableDescription("<summary>Field description original.</summary>");
        var methodDescription = new MutableDescription("<summary>Method description original.</summary>");
        var returnDescription = new MutableDescription("Return description original.");
        var parameterDescription = new MutableDescription("Parameter description original.");
        CurrentDescriptions =
        [
            recordDescription,
            fieldDescription,
            methodDescription,
            returnDescription,
            parameterDescription,
        ];
        var record = new RecordModel
        {
            TemplateProjection = new()
            {
                ManagedPack = 8,
                FixedTypeName = "FixtureValue",
                NativeTemplateName = "fixture::box",
                NativeTypePattern = "fixture::box<T>",
                FamilyName = "FixtureBox",
                DeclarationTypeName = "FixtureBox<T>",
                ClosedTypeName = "FixtureBox<int>",
                Arguments =
                [
                    new()
                    {
                        ParameterName = "T",
                        NativeArgument = "int",
                        ClosedCSharpType = "int",
                        Kind = TemplateArgumentProjectionKind.Generic,
                    },
                ],
            },
            DescriptionItems = [recordDescription,],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            SourceHeader = "fixture_value.hxx",
            Type = new()
            {
                CppTypeName = "fixture::value",
                CppValueTypeName = "fixture::value",
                CSharpPInvokeType = new("FixtureValue"),
                CSharpPublicType = new("FixtureValue"),
                IsRecord = true,
            },
            Size = 8,
            Alignment = 8,
            ObjectKind = NativeObjectKind.Value,
            FieldModels =
            [
                new()
                {
                    DescriptionItems = [fieldDescription,], Offset = 0, Size = 4, Alignment = 4, Name = "Value", Type = scalar,
                },
            ],
            MethodModels = exports.Select((export, index) => new MethodModel
            {
                DescriptionItems = index == 0 ? [methodDescription,] : [],
                ReturnTypeDescriptionItems = index == 0 ? [returnDescription,] : [],
                NativeExportName = export,
                NoExceptions = true,
                IsConst = false,
                IsStatic = true,
                ReturnType = scalar,
                MethodName = "Operation" + index,
                Type = MethodModelType.NORMAL,
                Parameters = index == 0
                    ? [new() { DescriptionItems = [parameterDescription,], Type = scalar, Name = "input", },]
                    : [],
            }).ToArray(),
        };
        CurrentRecord = record;
        var declaration = new BindingDeclaration(record, "fixture::value", isRoot: true, dependencies: []);
        var declarations = new[] { declaration };
        var model = new BindingProviderModel(
            declarations,
            [],
            CreateEmissionProfile(),
            managed,
            [new("fixture-native.txt", (writer, token) => RenderAsync(writer, "fixture native metadata", token)),],
            [],
            "managed-source-stem",
            "native-source-stem",
            nativeProject: null);
        declarations[0] = null!;
        managed.Clear();
        exports[0] = "MutatedAfterSnapshot";
        return Task.FromResult(model);
    }

    private static BindingEmissionProfile CreateEmissionProfile()
    {
        return new()
        {
            CSharpNamespace = "Independent.Generated",
            ManagedNativeErrorProjection = "global::Independent.Generated.NativeErrorProjection",
            NativeErrorClearExport = "NativeError_Clear",
            NativePreamble = "// <auto-generated />\n#include <new>\n#include <utility>\n",
            NativeErrorPreamble = "#include \"NativeError.h\"\n#include <exception>\n",
        };
    }

    private async Task RenderAsync(TextWriter writer, string contents, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref RenderCalls);
        Program.Require(Prepared && PlanCalls == 1, "Renderer observed an incomplete or repeated plan.");
        if (Scenario == "render_failure") throw new InvalidOperationException("fixture render failure");
        if (Scenario == "render_cancellation") throw new OperationCanceledException("fixture render cancellation", new CancellationToken(true));
        await writer.WriteAsync(contents.AsMemory(), cancellationToken);
    }

    private static BindingSemanticEngine CreateSemanticEngine(string scenario)
    {
        return new(
            [new TypeRule(),],
            new LayoutPolicy(),
            new TemplatePolicy(),
            [
                new SourceStemEmitter("managed-source-stem", value => GetManagedSourceStem(scenario, value)),
                new SourceStemEmitter("native-source-stem", value => GetNativeSourceStem(scenario, value)),
            ]);
    }

    private static string GetManagedSourceStem(string scenario, string value)
    {
        return scenario switch
        {
            "path_escape" => "../escape",
            "absolute_path" => Path.Combine(Path.GetTempPath(), "escape"),
            "reserved_managed" => "NativeApi",
            "device_path" => "NUL",
            _ => "Provider",
        };
    }

    private static string GetNativeSourceStem(string scenario, string value)
    {
        return scenario == "reserved_native" ? "NativeFunctionTable" : "Provider";
    }

    private sealed class TypeRule : IBindingTypeRule
    {
        public bool TryResolve(CppTypeDescriptor type, out BindingTypeProjection? projection)
        {
            projection = type.NativeName switch
            {
                "fixture::value" => new(type.NativeName, "FixtureValue"),
                "int" => new(type.NativeName, "int"),
                _ => null,
            };
            return projection is not null;
        }
    }

    private sealed class LayoutPolicy : IBindingLayoutPolicy
    {
        public BindingAdmission Admit(BindingLayoutDescriptor layout)
        {
            return layout.Alignment <= 8
                ? BindingAdmission.Admitted
                : BindingAdmission.Reject("Unsupported fixture alignment.");
        }
    }

    private sealed class TemplatePolicy : IBindingTemplatePolicy
    {
        public BindingAdmission Admit(BindingTemplateDescriptor template)
        {
            return template.IsClosed
                ? BindingAdmission.Admitted
                : BindingAdmission.Reject("The fixture admits closed templates only.");
        }
    }

    private sealed class SourceStemEmitter(string name, Func<string, string> emit) : IBindingEmitterPrimitive
    {
        public string Name => name;

        public string Emit(string value)
        {
            return emit(value);
        }
    }

    private sealed class MutableDescription(string text) : IDescriptionItem, IRootDescriptionItem
    {
        public int Calls { get; private set; }

        public string Text { get; set; } = text;

        public void ToDescription(ref SourceBuilder builder)
        {
            Calls++;
            builder.Append(Text);
        }
    }
}
