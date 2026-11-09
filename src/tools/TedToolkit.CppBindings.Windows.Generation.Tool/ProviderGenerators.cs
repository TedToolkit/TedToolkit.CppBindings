// -----------------------------------------------------------------------
// <copyright file="ProviderGenerators.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using ModularPipelines;
using ModularPipelines.Enums;

using TedToolkit.CppBindings.Cgal.Generator;
using TedToolkit.CppBindings.Fcl.Generator;
using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Manifold.Generator;
using TedToolkit.CppBindings.Occt.Generator;
using TedToolkit.CppBindings.Smlib.Generator;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal interface IGenerationOperations
{
    void EnsureDiskBoundary(string output, string stage);

    Task GenerateAsync(
        ProviderDescriptor descriptor,
        string output,
        string? vcpkg,
        CancellationToken cancellationToken);

    Task<string> BuildNativeAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string? vcpkg,
        string configuration,
        CancellationToken cancellationToken);
}

internal static class ProviderGenerators
{
    internal static Task GenerateAsync(
        string provider,
        DirectoryPath outputRoot,
        DirectoryPath repositoryRoot,
        string? vcpkgRoot,
        CancellationToken cancellationToken)
    {
        if (provider is "smlib")
        {
            var options = new GenerationOptions
            {
                CSharpFolder = outputRoot / "csharp",
                CppFolder = outputRoot / "cpp",
                CSharpNamespace = "TedToolkit.CppBindings.Smlib",
                NativeLibraryBaseName = SmlibGenerationProvider.NativeLibraryBaseName,
                CppVersion = 17,
            };
            return RunFinitePipelineAsync(
                options, new SmlibGenerationProvider(repositoryRoot / "externals" / "SMLib"), "SMLib", cancellationToken);
        }

        var vcpkg = new DirectoryPath(vcpkgRoot ?? throw new ArgumentNullException(nameof(vcpkgRoot)));
        return provider switch
        {
            "occt" => GenerateOcctAsync(outputRoot, vcpkg, cancellationToken),
            "cgal" => GenerateCgalAsync(outputRoot, vcpkg, cancellationToken),
            "manifold" => GenerateManifoldAsync(outputRoot, vcpkg, cancellationToken),
            "fcl" => GenerateFclAsync(outputRoot, vcpkg, cancellationToken),
            _ => throw new ArgumentException($"Unknown Windows provider '{provider}'.", nameof(provider)),
        };
    }

    internal static async Task<GenerationPlan> RunFinitePipelineAsync(
        GenerationOptions options,
        IGenerationProvider provider,
        string name,
        CancellationToken cancellationToken)
    {
        var recordingProvider = new PlanRecordingProvider(provider);
        var builder = CreateGenerationBuilder().AddCppGenerators(options, recordingProvider);
        await RunPipelineAsync(builder, name, cancellationToken).ConfigureAwait(false);
        return await (recordingProvider.Plan
            ?? throw new InvalidOperationException($"The {name} generator did not create a plan.")).ConfigureAwait(false);
    }

    private static PipelineBuilder CreateGenerationBuilder()
    {
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        return builder;
    }

    private static async Task RunPipelineAsync(PipelineBuilder builder, string name, CancellationToken cancellationToken)
    {
        var pipeline = await builder.BuildAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var summary = await pipeline.RunAsync().ConfigureAwait(false);
        if (summary.Status is not Status.Successful)
        {
            throw new InvalidOperationException($"The {name} generator did not complete successfully.");
        }
    }

    private static async Task GenerateOcctAsync(
        DirectoryPath outputRoot,
        DirectoryPath vcpkgRoot,
        CancellationToken cancellationToken)
    {
        using var environment = ProcessEnvironment.Override("VCPKG_ROOT", vcpkgRoot.FullName);
        var builder = CreateGenerationBuilder().AddOcctGenerators(
            new()
            {
                DeclOptions = [],
                GenerateAllPublicHeaders = true,
                CSharpFolder = outputRoot / "csharp",
                CppFolder = outputRoot / "cpp",
                CommandLineArgs = ["-w",],
                NativeLibraryVersion = new(8, 0, 1),
            });
        await RunPipelineAsync(builder, "OCCT", cancellationToken).ConfigureAwait(false);
    }

    private static async Task GenerateCgalAsync(
        DirectoryPath outputRoot,
        DirectoryPath vcpkgRoot,
        CancellationToken cancellationToken)
    {
        var options = new CgalGenerationOptions
        {
            VcpkgRoot = vcpkgRoot,
            CSharpFolder = outputRoot / "csharp",
            CppFolder = outputRoot / "cpp",
            CSharpNamespace = "TedToolkit.CppBindings.Cgal",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
            CppVersion = 20,
            NativeLibraryVersion = new(6, 2),
        };
        var provider = new CgalGenerationProvider(options);
        var plan = await RunFinitePipelineAsync(options, provider, "CGAL", cancellationToken).ConfigureAwait(false);

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
        await (outputRoot / new FileName("generation-result.json")).WriteAllTextAsync(
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task GenerateFclAsync(
        DirectoryPath outputRoot,
        DirectoryPath vcpkgRoot,
        CancellationToken cancellationToken)
    {
        var options = new GenerationOptions
        {
            CSharpFolder = outputRoot / "csharp",
            CppFolder = outputRoot / "cpp",
            CSharpNamespace = "TedToolkit.CppBindings.Fcl",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_fcl",
            CppVersion = 20,
            NativeLibraryVersion = new(0, 7, 0),
        };
        var provider = new FclGenerationProvider(vcpkgRoot);
        var plan = await RunFinitePipelineAsync(options, provider, "FCL", cancellationToken).ConfigureAwait(false);

        var result = new
        {
            provider.Profile.ProfileId,
            ManagedArtifactCount = plan.CSharpSources.Count + 1,
            NativeArtifactCount = plan.CppSources.Count + 1,
            NativeFunctionCount = plan.NativeExports.Count,
            Toolchain = provider.Profile.Versions,
        };
        await (outputRoot / new FileName("generation-result.json")).WriteAllTextAsync(
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task GenerateManifoldAsync(
        DirectoryPath outputRoot,
        DirectoryPath vcpkgRoot,
        CancellationToken cancellationToken)
    {
        var provider = new ManifoldGenerationProvider(vcpkgRoot);
        var options = new GenerationOptions
        {
            CSharpFolder = outputRoot / "csharp",
            CppFolder = outputRoot / "cpp",
            CSharpNamespace = "TedToolkit.CppBindings.Manifold",
            NativeLibraryBaseName = provider.Profile.NativeLibraryBaseName,
            CppVersion = 20,
            NativeLibraryVersion = new(3, 5, 2),
        };
        var plan = await RunFinitePipelineAsync(options, provider, "Manifold", cancellationToken).ConfigureAwait(false);

        var result = new
        {
            provider.Profile.ProfileId,
            ManagedArtifactCount = plan.CSharpSources.Count + 1,
            NativeArtifactCount = plan.CppSources.Count + 1,
            NativeExportCount = plan.NativeExports.Count,
            Toolchain = new
            {
                provider.Profile.CMake,
                provider.Profile.Msvc,
                provider.Profile.Triplet,
            },
        };
        await (outputRoot / new FileName("generation-result.json")).WriteAllTextAsync(
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class PlanRecordingProvider(IGenerationProvider provider) : IGenerationProvider
    {
        internal Task<GenerationPlan>? Plan { get; private set; }

        public IReadOnlyList<Type> PreparationModules => provider.PreparationModules;

        public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken) =>
            Plan = provider.CreatePlanAsync(cancellationToken);

        public Task<GenerationPlan> CreatePlanAsync(in GenerationOptions options, in CancellationToken cancellationToken) =>
            Plan = provider.CreatePlanAsync(options, cancellationToken);
    }
}

internal sealed class ProcessEnvironment : IDisposable
{
    private readonly string _name;
    private readonly string? _originalValue;

    private ProcessEnvironment(string name, string value)
    {
        _name = name;
        _originalValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }

    internal static ProcessEnvironment Override(string name, string value) => new(name, value);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _originalValue, EnvironmentVariableTarget.Process);
    }
}
