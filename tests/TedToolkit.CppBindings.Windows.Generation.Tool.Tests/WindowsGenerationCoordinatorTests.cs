// -----------------------------------------------------------------------
// <copyright file="WindowsGenerationCoordinatorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.CppBindings.Windows.Generation.Tool.Tests;

internal sealed class WindowsGenerationCoordinatorTests
{
    private static readonly string[] Providers = ["occt", "cgal", "manifold", "fcl"];

    [Test]
    public async Task Should_define_every_provider_in_the_shared_host_Async()
    {
        var descriptors = Providers.Select(ProviderDescriptor.Create).ToArray();

        await Assert.That(descriptors.Select(static item => item.Id)).IsEquivalentTo(Providers);
        await Assert.That(descriptors.Select(static item => item.NativeLibraryName).Distinct().Count()).IsEqualTo(4);
        await Assert.That(descriptors.All(static item => item.RepositoryInputRoots.Any(
            static root => root is "src/tools/TedToolkit.CppBindings.Windows.Generation.Tool"))).IsTrue();
        await Assert.That(ProviderDescriptor.Create("cgal").VcpkgInputRoots)
            .IsEquivalentTo(["installed/x64-windows/include/CGAL",]);
        await Assert.That(ProviderDescriptor.Create("cgal").VcpkgInputPatterns)
            .IsEquivalentTo([new VcpkgInputPattern("installed/vcpkg/info", "cgal_*.list"),]);
    }

    [Test]
    public async Task Should_parse_the_common_command_contract_Async()
    {
        var options = GenerationCommandLine.Parse(
        [
            "--provider", "cgal",
            "--repository-root", "repo",
            "--output-root", "output",
            "--vcpkg-root", "vcpkg",
            "--configuration", "Release",
        ]);

        await Assert.That(options.Provider).IsEqualTo("cgal");
        await Assert.That(options.Configuration).IsEqualTo("Release");
        await Assert.That(() => GenerationCommandLine.Parse(["--provider", "cgal", "--provider", "fcl"]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_reject_output_roots_outside_repository_evidence_roots_Async()
    {
        using var fixture = new TestDirectory();
        var validOutput = Path.Combine(fixture.Path, "output", "providers", "fcl");
        WindowsGenerationCoordinator.EnsureOwnedOutputRoot(fixture.Path, validOutput);

        await Assert.That(() => WindowsGenerationCoordinator.EnsureOwnedOutputRoot(
                fixture.Path,
                Path.Combine(fixture.Path, "src", "generated")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Should_enforce_both_disk_guards_Async()
    {
        const long gibibyte = 1024L * 1024 * 1024;
        WindowsGenerationCoordinator.EnsureDiskBoundary(25 * gibibyte, 12 * gibibyte, "boundary");

        await Assert.That(() => WindowsGenerationCoordinator.EnsureDiskBoundary(
                (25 * gibibyte) - 1,
                0,
                "free-space"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => WindowsGenerationCoordinator.EnsureDiskBoundary(
                100 * gibibyte,
                (12 * gibibyte) + 1,
                "scratch"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Should_invoke_both_disk_guards_from_the_real_coordinator_Async()
    {
        using var fixture = new CacheFixture("fcl");
        await fixture.PrepareInputsAsync();
        var generationFailure = new FixtureGenerationOperations { FailingDiskStage = "FCL generation" };

        await Assert.That(async () =>
            {
                await fixture.CreateCoordinator(new FixtureNativeClosure(), generationFailure)
                    .RunAsync(fixture.Options, CancellationToken.None);
            })
            .Throws<InvalidOperationException>();
        await Assert.That(generationFailure.DiskStages).IsEquivalentTo(["FCL generation",]);

        var nativeFailure = new FixtureGenerationOperations { FailingDiskStage = "FCL native compilation" };
        await Assert.That(async () =>
            {
                await fixture.CreateCoordinator(new FixtureNativeClosure(), nativeFailure)
                    .RunAsync(fixture.Options, CancellationToken.None);
            })
            .Throws<InvalidOperationException>();
        await Assert.That(nativeFailure.DiskStages)
            .IsEquivalentTo(["FCL generation", "FCL native compilation",]);
        await Assert.That(nativeFailure.BuildCount).IsEqualTo(0);
        await Assert.That(File.Exists(fixture.Stamp)).IsFalse();
    }

    [Test]
    public async Task Should_execute_each_provider_once_and_then_reuse_the_complete_cache_Async()
    {
        foreach (var provider in Providers)
        {
            using var fixture = new CacheFixture(provider);
            await fixture.PrepareInputsAsync();
            var operations = new FixtureGenerationOperations();
            var closure = new FixtureNativeClosure();
            var coordinator = fixture.CreateCoordinator(closure, operations);

            await coordinator.RunAsync(fixture.Options, CancellationToken.None);
            await coordinator.RunAsync(fixture.Options, CancellationToken.None);

            await Assert.That(operations.GeneratedProviders).IsEquivalentTo([provider,]);
            await Assert.That(operations.BuildCount).IsEqualTo(1);
            await Assert.That(operations.DiskStages.Count).IsEqualTo(2);
            await Assert.That(File.Exists(fixture.Stamp)).IsTrue();
            await Assert.That(Directory.Exists(fixture.OcctTargetObjects)).IsFalse();
        }
    }

    [Test]
    public async Task Should_preserve_existing_compiler_options_when_enabling_large_objects_Async()
    {
        var original = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CL"] = "/EHsc",
            ["PATH"] = "compiler-path",
        };

        var augmented = WindowsGenerationCoordinator.WithCompilerOption(original, "/bigobj");
        var repeated = WindowsGenerationCoordinator.WithCompilerOption(augmented, "/BIGOBJ");

        await Assert.That(original["CL"]).IsEqualTo("/EHsc");
        await Assert.That(augmented["CL"]).IsEqualTo("/EHsc /bigobj");
        await Assert.That(augmented["PATH"]).IsEqualTo("compiler-path");
        await Assert.That(repeated["CL"]).IsEqualTo("/EHsc /bigobj");
    }

    [Test]
    [Arguments("fcl")]
    [Arguments("manifold")]
    public async Task Should_reject_a_mismatched_locked_compiler_without_publishing_outputs_Async(string provider)
    {
        using var fixture = new CacheFixture(provider);
        await fixture.PrepareInputsAsync();
        var operations = new FixtureGenerationOperations { RejectCompilerIdentity = true };
        InvalidOperationException? failure = null;
        try
        {
            await fixture.CreateCoordinator(new FixtureNativeClosure(), operations)
                .RunAsync(fixture.Options, CancellationToken.None);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message)
            .IsEqualTo("CMake selected compiler 'MSVC|19.51.36256.0|C:\\fake\\cl.exe', which does not match the locked profile.");
        await Assert.That(File.Exists(fixture.NativeLibrary)).IsFalse();
        await Assert.That(File.Exists(fixture.Stamp)).IsFalse();
    }

    [Test]
    public async Task Should_not_publish_a_stamp_when_native_dependency_staging_fails_Async()
    {
        using var fixture = new CacheFixture("cgal");
        await fixture.PrepareInputsAsync();
        var closure = new FixtureNativeClosure { FailStaging = true };

        await Assert.That(async () =>
            {
                await fixture.CreateCoordinator(closure, new FixtureGenerationOperations())
                    .RunAsync(fixture.Options, CancellationToken.None);
            })
            .Throws<InvalidOperationException>();

        await Assert.That(File.Exists(fixture.Stamp)).IsFalse();
    }

    [Test]
    public async Task Should_use_one_named_lock_per_provider_output_Async()
    {
        using var fixture = new TestDirectory();
        var descriptor = ProviderDescriptor.Create("occt");
        var output = Path.Combine(fixture.Path, "output", "generated");
        var name = WindowsGenerationCoordinator.GetMutexName(descriptor, output);
        using var ownerReady = new ManualResetEvent(initialState: false);
        using var releaseOwner = new ManualResetEvent(initialState: false);
        var ownerTask = Task.Run(() =>
        {
            using var owner = new Mutex(initiallyOwned: true, name);
            ownerReady.Set();
            releaseOwner.WaitOne();
            owner.ReleaseMutex();
        });
        ownerReady.WaitOne();
        using var second = new Mutex(initiallyOwned: false, name);
        var secondAcquired = second.WaitOne(0);
        releaseOwner.Set();
        await ownerTask;

        await Assert.That(secondAcquired).IsFalse();
        await Assert.That(WindowsGenerationCoordinator.GetMutexName(descriptor, output)).IsEqualTo(name);
        await Assert.That(WindowsGenerationCoordinator.GetMutexName(
            ProviderDescriptor.Create("fcl"), output)).IsEqualTo(name);
        await Assert.That(WindowsGenerationCoordinator.GetMutexName(
            descriptor, output.ToUpperInvariant())).IsEqualTo(name);
        await Assert.That(WindowsGenerationCoordinator.GetMutexName(
            descriptor, Path.Combine(fixture.Path, "output", "providers", "cgal"))).IsNotEqualTo(name);
    }

    [Test]
    public async Task Should_serialize_real_coordinator_runs_for_the_same_output_Async()
    {
        using var fixture = new CacheFixture("occt");
        await fixture.WriteValidCacheAsync();
        var closure = new BlockingNativeClosure();
        var first = fixture.CreateCoordinator(closure).RunAsync(fixture.Options, CancellationToken.None);
        await closure.FirstCallEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = fixture.CreateCoordinator(closure).RunAsync(fixture.Options, CancellationToken.None);
        await Task.Delay(100);
        await Assert.That(closure.CallCount).IsEqualTo(1);

        closure.ReleaseFirstCall.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(closure.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Should_restore_temporary_process_environment_Async()
    {
        var name = "TEDTOOLKIT_GENERATION_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "before", EnvironmentVariableTarget.Process);
        using (ProcessEnvironment.Override(name, "during"))
        {
            await Assert.That(Environment.GetEnvironmentVariable(name)).IsEqualTo("during");
        }

        await Assert.That(Environment.GetEnvironmentVariable(name)).IsEqualTo("before");
        Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
    }

    [Test]
    public async Task Should_propagate_process_failures_Async()
    {
        var commandInterpreter = Environment.GetEnvironmentVariable("COMSPEC")
            ?? throw new InvalidOperationException("COMSPEC is required for this Windows test.");
        var runner = new SystemProcessRunner();

        await Assert.That(async () =>
            {
                _ = await runner.RunAsync(
                    commandInterpreter,
                    ["/d", "/c", "exit 7",],
                    Environment.CurrentDirectory,
                    null,
                    throwOnError: true,
                    CancellationToken.None);
            })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Should_read_a_compiler_environment_from_a_script_with_spaces_Async()
    {
        using var fixture = new TestDirectory();
        var scriptDirectory = Path.Combine(fixture.Path, "compiler environment");
        var script = Path.Combine(scriptDirectory, "fixture environment.cmd");
        var variable = "TEDTOOLKIT_COMPILER_ENVIRONMENT_" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(script, $"@set {variable}=available{Environment.NewLine}");
        var coordinator = new WindowsGenerationCoordinator(
            new SystemProcessRunner(),
            new FixtureNativeClosure());

        var environment = await coordinator.ReadCompilerEnvironmentAsync(
            script,
            fixture.Path,
            CancellationToken.None);

        await Assert.That(environment[variable]).IsEqualTo("available");
    }

    [Test]
    public async Task Should_pass_arguments_to_the_powershell_dependency_module_Async()
    {
        using var fixture = new TestDirectory();
        var moduleDirectory = Path.Combine(fixture.Path, "dependency module");
        var module = Path.Combine(moduleDirectory, "Fixture Closure.psm1");
        var marker = Path.Combine(fixture.Path, "closure-marker.txt");
        Directory.CreateDirectory(moduleDirectory);
        await File.WriteAllTextAsync(
            module,
            """
            function Test-NativeDependencyClosure {
                param($NativeLibrary, $Destination, $Manifest)
                return -not [string]::IsNullOrWhiteSpace($NativeLibrary) `
                    -and -not [string]::IsNullOrWhiteSpace($Destination) `
                    -and -not [string]::IsNullOrWhiteSpace($Manifest)
            }

            function Get-WindowsNativeToolchain {
                param($Compiler)
                if ([string]::IsNullOrWhiteSpace($Compiler)) { throw 'Compiler is required.' }
                return $Compiler
            }

            function Set-NativeDependencyClosure {
                param($NativeLibrary, $Destination, $VcpkgBin, $Toolchain, $OwnedRoot)
                if (@($NativeLibrary, $Destination, $VcpkgBin, $Toolchain, $OwnedRoot) |
                    Where-Object { [string]::IsNullOrWhiteSpace($_) }) {
                    throw 'Every closure argument is required.'
                }

                Set-Content -LiteralPath (Join-Path $OwnedRoot 'closure-marker.txt') -Value 'complete'
            }

            Export-ModuleMember -Function Test-NativeDependencyClosure, Get-WindowsNativeToolchain, Set-NativeDependencyClosure
            """);
        var closure = new PowerShellNativeDependencyClosure(new SystemProcessRunner());

        var complete = await closure.IsCompleteAsync(
            module,
            "native library",
            "dependency destination",
            "dependency manifest",
            fixture.Path,
            CancellationToken.None);
        await closure.StageAsync(
            module,
            "compiler path",
            "native library",
            "dependency destination",
            "vcpkg bin",
            fixture.Path,
            fixture.Path,
            CancellationToken.None);

        await Assert.That(complete).IsTrue();
        await Assert.That(File.Exists(marker)).IsTrue();
    }

    [Test]
    public async Task Should_invalidate_a_success_stamp_before_retry_Async()
    {
        using var fixture = new TestDirectory();
        var stamp = Path.Combine(fixture.Path, "generation.stamp");
        await File.WriteAllTextAsync(stamp, "success");

        WindowsGenerationCoordinator.InvalidateStamp(stamp);

        await Assert.That(File.Exists(stamp)).IsFalse();
    }

    [Test]
    public async Task Should_not_retain_a_success_stamp_when_generation_fails_Async()
    {
        using var fixture = new CacheFixture("fcl");
        await fixture.WriteValidCacheAsync();
        await File.AppendAllTextAsync(fixture.InputFile, "changed");
        var operations = new FixtureGenerationOperations { FailGeneration = true };

        await Assert.That(async () =>
            {
                await fixture.CreateCoordinator(new FixtureNativeClosure(), operations)
                    .RunAsync(fixture.Options, CancellationToken.None);
            })
            .Throws<DirectoryNotFoundException>();

        await Assert.That(File.Exists(fixture.Stamp)).IsFalse();
    }

    [Test]
    public async Task Should_reset_only_the_owned_native_build_directory_Async()
    {
        using var fixture = new TestDirectory();
        var output = Path.Combine(fixture.Path, "output", "providers", "fcl");
        var nativeFile = Path.Combine(output, "native-build", "stale.obj");
        var managedFile = Path.Combine(output, "csharp", "Fcl.Bindings.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(nativeFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(managedFile)!);
        await File.WriteAllTextAsync(nativeFile, "stale");
        await File.WriteAllTextAsync(managedFile, "managed");

        WindowsGenerationCoordinator.ResetNativeBuildDirectory(output);

        await Assert.That(Directory.Exists(Path.Combine(output, "native-build"))).IsFalse();
        await Assert.That(File.Exists(managedFile)).IsTrue();
    }

    [Test]
    public async Task Should_reclaim_only_the_occt_target_object_directory_Async()
    {
        using var fixture = new TestDirectory();
        var output = Path.Combine(fixture.Path, "output", "generated");
        var target = Path.Combine(output, "native-build", "CMakeFiles", "ted_toolkit_occt.dir", "fixture.obj");
        var retained = Path.Combine(output, "native-build", "CMakeFiles", "retained.dir", "fixture.obj");
        foreach (var file in new[] { target, retained, })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "object");
        }

        WindowsGenerationCoordinator.ReclaimNativeBuildIntermediates(ProviderDescriptor.Create("occt"), output);

        await Assert.That(File.Exists(target)).IsFalse();
        await Assert.That(File.Exists(retained)).IsTrue();
    }

    [Test]
    public async Task Should_remove_stale_generated_sources_without_touching_other_outputs_Async()
    {
        using var fixture = new TestDirectory();
        var output = Path.Combine(fixture.Path, "output", "providers", "cgal");
        var managedFile = Path.Combine(output, "csharp", "Stale.g.cs");
        var nativeSource = Path.Combine(output, "cpp", "Stale.cpp");
        var retainedFile = Path.Combine(output, "native-dependencies", "dependency.dll");
        foreach (var file in new[] { managedFile, nativeSource, retainedFile, })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "content");
        }

        WindowsGenerationCoordinator.ResetGeneratedSourceDirectories(output);

        await Assert.That(File.Exists(managedFile)).IsFalse();
        await Assert.That(File.Exists(nativeSource)).IsFalse();
        await Assert.That(File.Exists(retainedFile)).IsTrue();
    }

    [Test]
    public async Task Should_accept_only_complete_unmodified_caches_for_every_provider_Async()
    {
        foreach (var provider in Providers)
        {
            await VerifyCacheInvalidationAsync(provider);
        }
    }

    private static async Task VerifyCacheInvalidationAsync(string provider)
    {
        using var fixture = new CacheFixture(provider);
        await fixture.WriteValidCacheAsync();
        await Assert.That(await fixture.IsCompleteAsync()).IsTrue();

        File.Delete(fixture.ManagedFile);
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        await File.WriteAllTextAsync(fixture.ManagedFile, string.Empty);
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        File.Delete(fixture.NativeLibrary);
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        await File.AppendAllTextAsync(fixture.NativeLibrary, "modified");
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        await File.AppendAllTextAsync(fixture.InputFile, "changed");
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        File.Delete(fixture.InputFile);
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await File.WriteAllTextAsync(fixture.InputFile, "input");
        await fixture.WriteValidCacheAsync();

        await File.AppendAllTextAsync(fixture.NativeClosureModule, "changed");
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        if (fixture.VcpkgHeader is not null)
        {
            await File.AppendAllTextAsync(fixture.VcpkgHeader, "changed");
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();

            File.Delete(fixture.VcpkgHeader);
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();
        }

        if (fixture.VcpkgPackageList is not null)
        {
            await File.AppendAllTextAsync(fixture.VcpkgPackageList, "changed");
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();

            File.Delete(fixture.VcpkgPackageList);
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();
        }

        if (fixture.ManagedManifest is not null)
        {
            File.Delete(fixture.ManagedManifest);
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();
        }

        if (fixture.OutputManifest is not null)
        {
            await File.WriteAllTextAsync(fixture.OutputManifest, "{}");
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();
        }

        File.Delete(fixture.DependencyManifest);
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        await File.AppendAllTextAsync(fixture.DependencyLibrary, "changed");
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        fixture.NativeClosure.Complete = false;
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
        await fixture.WriteValidCacheAsync();

        if (fixture.NoticeSource is not null && fixture.NoticeDestination is not null)
        {
            await File.AppendAllTextAsync(fixture.NoticeSource, "changed");
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();

            await File.AppendAllTextAsync(fixture.NoticeDestination, "changed");
            await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
            await fixture.WriteValidCacheAsync();
        }

        await File.AppendAllTextAsync(fixture.ManagedFile, "modified");
        await Assert.That(await fixture.IsCompleteAsync()).IsFalse();
    }

    private sealed class CacheFixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        private readonly ProviderDescriptor descriptor;
        private readonly WindowsGenerationCoordinator coordinator;

        internal CacheFixture(string provider)
        {
            descriptor = ProviderDescriptor.Create(provider);
            NativeClosure = new();
            coordinator = CreateCoordinator(NativeClosure);
            Repository = directory.Path;
            Output = Path.Combine(Repository, "output", "providers", provider);
            Vcpkg = Path.Combine(Repository, "vcpkg");
            InputFile = Path.Combine(Repository, "Directory.Build.props");
            NativeClosureModule = Path.Combine(Repository, "Build", "NativeDependencyClosure.psm1");
            ManagedFile = Path.Combine(
                Output,
                "csharp",
                descriptor.RequiredManagedFile ?? "Bindings.g.cs");
            NativeLibrary = Path.Combine(Output, "native-build", "Release", descriptor.NativeLibraryName);
            ManagedManifest = descriptor.WriteManagedManifest
                ? Path.Combine(Output, "native-build", "Release", "managed-files.txt")
                : null;
            OutputManifest = descriptor.WriteOutputManifest
                ? Path.Combine(Output, "output-manifest.json")
                : null;
            DependencyManifest = Path.Combine(Output, "native-dependencies.json");
            DependencyLibrary = Path.Combine(Output, "native-dependencies", "fixture.dll");
            VcpkgHeader = descriptor.VcpkgInputRoots.Count is 0
                ? null
                : Path.Combine(Vcpkg, descriptor.VcpkgInputRoots[0].Replace('/', Path.DirectorySeparatorChar), "Fixture.hpp");
            VcpkgPackageList = descriptor.VcpkgInputPatterns.Count is 0
                ? null
                : Path.Combine(
                    Vcpkg,
                    descriptor.VcpkgInputPatterns[0].RelativeRoot.Replace('/', Path.DirectorySeparatorChar),
                    descriptor.VcpkgInputPatterns[0].SearchPattern.Replace("*", "fixture", StringComparison.Ordinal));
            var firstNotice = descriptor.Notices.FirstOrDefault();
            NoticeSource = firstNotice.Key is null
                ? null
                : Path.Combine(Vcpkg, firstNotice.Value.Replace('/', Path.DirectorySeparatorChar));
            NoticeDestination = firstNotice.Key is null
                ? null
                : Path.Combine(Output, "third-party-notices", firstNotice.Key);
        }

        internal string Repository { get; }

        internal string Output { get; }

        internal string Vcpkg { get; }

        internal string InputFile { get; }

        internal string NativeClosureModule { get; }

        internal string ManagedFile { get; }

        internal string NativeLibrary { get; }

        internal string? ManagedManifest { get; }

        internal string? OutputManifest { get; }

        internal string DependencyManifest { get; }

        internal string DependencyLibrary { get; }

        internal string? VcpkgHeader { get; }

        internal string? VcpkgPackageList { get; }

        internal string? NoticeSource { get; }

        internal string? NoticeDestination { get; }

        internal FixtureNativeClosure NativeClosure { get; }

        internal WindowsGenerationOptions Options => new(
            descriptor.Id,
            new(Repository),
            new(Output),
            new(Vcpkg),
            "Release");

        internal WindowsGenerationCoordinator CreateCoordinator(INativeDependencyClosure closure) =>
            new(new UnusedProcessRunner(), closure);

        internal WindowsGenerationCoordinator CreateCoordinator(
            INativeDependencyClosure closure,
            IGenerationOperations generationOperations) =>
            new(new UnusedProcessRunner(), closure, generationOperations);

        internal string Stamp => Path.Combine(Output, "native-build", "Release", "generation.stamp");

        internal string OcctTargetObjects =>
            Path.Combine(Output, "native-build", "CMakeFiles", "ted_toolkit_occt.dir");

        internal async Task WriteValidCacheAsync()
        {
            await PrepareInputsAsync();
            Directory.CreateDirectory(Path.GetDirectoryName(ManagedFile)!);
            Directory.CreateDirectory(Path.GetDirectoryName(NativeLibrary)!);
            await File.WriteAllTextAsync(ManagedFile, "internal struct Binding { }");
            await File.WriteAllTextAsync(NativeLibrary, "native");
            Directory.CreateDirectory(Path.GetDirectoryName(DependencyLibrary)!);
            await File.WriteAllTextAsync(DependencyLibrary, "dependency");
            await File.WriteAllTextAsync(DependencyManifest, "fixture manifest");
            NativeClosure.Complete = true;

            var managedManifest = ManagedManifest
                ?? Path.Combine(Output, "native-build", "Release", "managed-files.txt");
            WindowsGenerationCoordinator.WriteManagedManifestIfRequired(descriptor, Output, managedManifest);
            var outputManifest = OutputManifest ?? Path.Combine(Output, "output-manifest.json");
            if (descriptor.WriteOutputManifest)
            {
                await WindowsGenerationCoordinator.WriteOutputManifestAsync(
                    descriptor,
                    Output,
                    outputManifest,
                    "Release",
                    CancellationToken.None);
            }

            var inputFingerprint = await WindowsGenerationCoordinator.ComputeInputFingerprintAsync(
                descriptor,
                Repository,
                Vcpkg,
                CancellationToken.None);
            var outputFingerprint = await WindowsGenerationCoordinator.ComputeOutputFingerprintAsync(
                descriptor,
                Output,
                "Release",
                CancellationToken.None);
            await File.WriteAllTextAsync(
                Stamp,
                JsonSerializer.Serialize(new { InputFingerprint = inputFingerprint, OutputFingerprint = outputFingerprint }));
        }

        internal async Task PrepareInputsAsync()
        {
            Directory.CreateDirectory(Path.Combine(Vcpkg, "installed", "vcpkg"));
            Directory.CreateDirectory(Path.GetDirectoryName(NativeClosureModule)!);
            await File.WriteAllTextAsync(InputFile, "input");
            await File.WriteAllTextAsync(NativeClosureModule, "closure module");
            await File.WriteAllTextAsync(Path.Combine(Vcpkg, "installed", "vcpkg", "status"), "status");
            if (VcpkgHeader is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VcpkgHeader)!);
                await File.WriteAllTextAsync(VcpkgHeader, "header");
            }

            if (VcpkgPackageList is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VcpkgPackageList)!);
                await File.WriteAllTextAsync(VcpkgPackageList, "package list");
            }

            foreach (var notice in descriptor.Notices)
            {
                var source = Path.Combine(Vcpkg, notice.Value.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                await File.WriteAllTextAsync(source, notice.Key);
                var destination = Path.Combine(Output, "third-party-notices", notice.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllTextAsync(destination, notice.Key);
            }
        }

        internal Task<bool> IsCompleteAsync() => coordinator.IsCompleteAsync(
            descriptor,
            Repository,
            Output,
            Vcpkg,
            "Release",
            CancellationToken.None);

        public void Dispose() => directory.Dispose();
    }

    private sealed class FixtureNativeClosure : INativeDependencyClosure
    {
        internal bool Complete { get; set; } = true;

        internal bool FailStaging { get; init; }

        public Task<bool> IsCompleteAsync(
            string module,
            string nativeLibrary,
            string destination,
            string manifest,
            string repository,
            CancellationToken cancellationToken) => Task.FromResult(
                Complete
                && File.Exists(Path.Combine(destination, "fixture.dll"))
                && File.Exists(manifest)
                && string.Equals(File.ReadAllText(manifest), "fixture manifest", StringComparison.Ordinal));

        public Task StageAsync(
            string module,
            string compiler,
            string nativeLibrary,
            string destination,
            string searchDirectory,
            string generatedRoot,
            string repository,
            CancellationToken cancellationToken)
        {
            if (FailStaging)
            {
                throw new InvalidOperationException("Synthetic native dependency staging failure.");
            }

            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "fixture.dll"), "dependency");
            File.WriteAllText(Path.Combine(generatedRoot, "native-dependencies.json"), "fixture manifest");
            return Task.CompletedTask;
        }
    }

    private sealed class FixtureGenerationOperations : IGenerationOperations
    {
        internal List<string> DiskStages { get; } = [];

        internal List<string> GeneratedProviders { get; } = [];

        internal string? FailingDiskStage { get; init; }

        internal bool FailGeneration { get; init; }

        internal bool RejectCompilerIdentity { get; init; }

        internal int BuildCount { get; private set; }

        public void EnsureDiskBoundary(string output, string stage)
        {
            DiskStages.Add(stage);
            if (string.Equals(stage, FailingDiskStage, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Synthetic disk boundary failure.");
            }
        }

        public async Task GenerateAsync(
            ProviderDescriptor descriptor,
            string output,
            string vcpkg,
            CancellationToken cancellationToken)
        {
            GeneratedProviders.Add(descriptor.Id);
            if (FailGeneration)
            {
                throw new DirectoryNotFoundException("Synthetic generation input is missing.");
            }

            var managed = Path.Combine(
                output,
                "csharp",
                descriptor.RequiredManagedFile ?? "Bindings.g.cs");
            var native = Path.Combine(output, "cpp", "Bindings.cpp");
            Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
            Directory.CreateDirectory(Path.GetDirectoryName(native)!);
            await File.WriteAllTextAsync(managed, "internal struct Binding { }", cancellationToken);
            await File.WriteAllTextAsync(native, "// native", cancellationToken);
        }

        public async Task<string> BuildNativeAsync(
            ProviderDescriptor descriptor,
            string repository,
            string output,
            string vcpkg,
            string configuration,
            CancellationToken cancellationToken)
        {
            BuildCount++;
            if (RejectCompilerIdentity)
            {
                WindowsGenerationCoordinator.EnsureCompilerMatchesLockedProfile(
                    "MSVC",
                    "19.51.36256.0",
                    "C:\\fake\\cl.exe",
                    "19.51.36257");
            }

            var native = Path.Combine(output, "native-build", configuration, descriptor.NativeLibraryName);
            Directory.CreateDirectory(Path.GetDirectoryName(native)!);
            await File.WriteAllTextAsync(native, "native", cancellationToken);
            if (descriptor.NativeBuild is NativeBuildKind.Occt)
            {
                var targetObject = Path.Combine(
                    output,
                    "native-build",
                    "CMakeFiles",
                    "ted_toolkit_occt.dir",
                    "fixture.obj");
                Directory.CreateDirectory(Path.GetDirectoryName(targetObject)!);
                await File.WriteAllTextAsync(targetObject, "object", cancellationToken);
            }

            return Path.Combine(repository, "fixture-compiler.exe");
        }
    }

    private sealed class BlockingNativeClosure : INativeDependencyClosure
    {
        private int callCount;

        internal TaskCompletionSource FirstCallEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int CallCount => Volatile.Read(ref callCount);

        public async Task<bool> IsCompleteAsync(
            string module,
            string nativeLibrary,
            string destination,
            string manifest,
            string repository,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref callCount) is 1)
            {
                FirstCallEntered.SetResult();
                await ReleaseFirstCall.Task.WaitAsync(cancellationToken);
            }

            return true;
        }

        public Task StageAsync(
            string module,
            string compiler,
            string nativeLibrary,
            string destination,
            string searchDirectory,
            string generatedRoot,
            string repository,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnusedProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string>? environment,
            bool throwOnError,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestDirectory : IDisposable
    {
        internal TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tedtoolkit-windows-generation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var temp = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(resolved).StartsWith(
                    "tedtoolkit-windows-generation-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to delete a directory outside the test root.");
            }

            if (Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }
}
