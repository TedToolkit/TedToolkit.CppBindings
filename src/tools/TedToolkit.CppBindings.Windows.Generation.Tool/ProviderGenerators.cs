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

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal interface IGenerationOperations
{
    void EnsureDiskBoundary(string output, string stage);

    Task GenerateAsync(
        ProviderDescriptor descriptor,
        string output,
        string vcpkg,
        CancellationToken cancellationToken);

    Task<string> BuildNativeAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken);
}

internal static class ProviderGenerators
{
    internal static async Task GenerateAsync(
        string provider,
        DirectoryInfo outputRoot,
        DirectoryInfo vcpkgRoot,
        CancellationToken cancellationToken)
    {
        switch (provider)
        {
            case "occt":
                await GenerateOcctAsync(outputRoot, vcpkgRoot, cancellationToken).ConfigureAwait(false);
                break;
            case "cgal":
                await GenerateCgalAsync(outputRoot, vcpkgRoot, cancellationToken).ConfigureAwait(false);
                break;
            case "manifold":
                await new ManifoldGenerationProvider().GenerateAsync(outputRoot, vcpkgRoot, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case "fcl":
                await GenerateFclAsync(outputRoot, vcpkgRoot, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentException($"Unknown Windows provider '{provider}'.", nameof(provider));
        }
    }

    private static async Task GenerateOcctAsync(
        DirectoryInfo outputRoot,
        DirectoryInfo vcpkgRoot,
        CancellationToken cancellationToken)
    {
        using var environment = ProcessEnvironment.Override("VCPKG_ROOT", vcpkgRoot.FullName);
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        var pipeline = await builder.AddOcctGenerators(
            new()
            {
                DeclOptions = [],
                GenerateAllPublicHeaders = true,
                CSharpFolder = outputRoot.CreateSubdirectory("csharp"),
                CppFolder = outputRoot.CreateSubdirectory("cpp"),
                CommandLineArgs = ["-w",],
            }).BuildAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var summary = await pipeline.RunAsync().ConfigureAwait(false);
        if (summary.Status is not Status.Successful)
        {
            throw new InvalidOperationException("The OCCT generator did not complete successfully.");
        }
    }

    private static async Task GenerateCgalAsync(
        DirectoryInfo outputRoot,
        DirectoryInfo vcpkgRoot,
        CancellationToken cancellationToken)
    {
        var options = new CgalGenerationOptions
        {
            VcpkgRoot = vcpkgRoot,
            CSharpFolder = outputRoot.CreateSubdirectory("csharp"),
            CppFolder = outputRoot.CreateSubdirectory("cpp"),
            CSharpNamespace = "TedToolkit.CppBindings.Cgal",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
            CppVersion = 20,
        };
        var provider = new CgalGenerationProvider(options);
        var plan = await provider.CreatePlanAsync(cancellationToken).ConfigureAwait(false);
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync().ConfigureAwait(false);
        var summary = await pipeline.RunAsync().ConfigureAwait(false);
        if (summary.Status is not Status.Successful)
        {
            throw new InvalidOperationException("The CGAL generator did not complete successfully.");
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
                Path.Combine(outputRoot.FullName, "generation-result.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task GenerateFclAsync(
        DirectoryInfo outputRoot,
        DirectoryInfo vcpkgRoot,
        CancellationToken cancellationToken)
    {
        var options = new GenerationOptions
        {
            CSharpFolder = outputRoot.CreateSubdirectory("csharp"),
            CppFolder = outputRoot.CreateSubdirectory("cpp"),
            CSharpNamespace = "TedToolkit.CppBindings.Fcl",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_fcl",
            CppVersion = 20,
        };
        var provider = new FclGenerationProvider(vcpkgRoot);
        var plan = await provider.CreatePlanAsync(cancellationToken).ConfigureAwait(false);
        var builder = Pipeline.CreateBuilder();
        builder.Options.PrintLogo = false;
        builder.Options.PrintResults = false;
        builder.Options.ShowProgressInConsole = false;
        builder.Options.DefaultRetryCount = 0;
        builder.Options.ThrowOnPipelineFailure = true;
        var pipeline = await builder.AddCppGenerators(options, provider).BuildAsync().ConfigureAwait(false);
        var summary = await pipeline.RunAsync().ConfigureAwait(false);
        if (summary.Status is not Status.Successful)
        {
            throw new InvalidOperationException("The FCL generator did not complete successfully.");
        }

        var result = new
        {
            provider.Profile.ProfileId,
            ManagedArtifactCount = plan.CSharpSources.Count + 1,
            NativeArtifactCount = plan.CppSources.Count + 1,
            NativeFunctionCount = plan.NativeExports.Count,
            Toolchain = provider.Profile.Versions,
        };
        await File.WriteAllTextAsync(
                Path.Combine(outputRoot.FullName, "generation-result.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                cancellationToken)
            .ConfigureAwait(false);
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