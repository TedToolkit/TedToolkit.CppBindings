// -----------------------------------------------------------------------
// <copyright file="WindowsGenerationCoordinator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TedToolkit.CppBindings.Windows.Generation.Tool;

internal sealed partial class WindowsGenerationCoordinator(
    IProcessRunner processes,
    INativeDependencyClosure nativeDependencies)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IGenerationOperations? operations;

    internal WindowsGenerationCoordinator(
        IProcessRunner processes,
        INativeDependencyClosure nativeDependencies,
        IGenerationOperations operations)
        : this(processes, nativeDependencies)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    internal async Task RunAsync(WindowsGenerationOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows binding generation requires Windows.");
        }

        var descriptor = ProviderDescriptor.Create(options.Provider.ToLowerInvariant());
        var repository = Path.GetFullPath(options.RepositoryRoot.FullName);
        var output = Path.GetFullPath(options.OutputRoot.FullName);
        var vcpkg = Path.GetFullPath(options.VcpkgRoot.FullName);
        EnsureOwnedOutputRoot(repository, output);

        var mutexName = GetMutexName(descriptor, output);
        await Task.Run(
                () => RunWithMutex(
                    mutexName,
                    descriptor,
                    repository,
                    output,
                    vcpkg,
                    options.Configuration,
                    cancellationToken),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private void RunWithMutex(
        string mutexName,
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(initiallyOwned: false, mutexName);
        var acquired = false;
        try
        {
            try
            {
                var signal = WaitHandle.WaitAny([mutex, cancellationToken.WaitHandle]);
                if (signal is 1)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                acquired = true;
            }
            catch (AbandonedMutexException exception) when (exception.MutexIndex is 0)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new InvalidOperationException($"Failed to acquire the {descriptor.DisplayName} generation lock.");
            }

            RunLockedAsync(descriptor, repository, output, vcpkg, configuration, cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private async Task RunLockedAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        var nativeBuild = Path.Combine(output, "native-build");
        var nativeLibrary = Path.Combine(nativeBuild, configuration, descriptor.NativeLibraryName);
        var stamp = Path.Combine(nativeBuild, configuration, "generation.stamp");
        var managedManifest = Path.Combine(nativeBuild, configuration, "managed-files.txt");
        var dependencyDirectory = Path.Combine(output, "native-dependencies");
        var dependencyManifest = Path.Combine(output, "native-dependencies.json");
        var outputManifest = Path.Combine(output, "output-manifest.json");
        var nativeModule = Path.Combine(repository, "Build", "NativeDependencyClosure.psm1");
        var fingerprint = await ComputeInputFingerprintAsync(descriptor, repository, vcpkg, cancellationToken)
            .ConfigureAwait(false);

        if (await IsCompleteAsync(
                descriptor,
                output,
                nativeLibrary,
                stamp,
                managedManifest,
                dependencyDirectory,
                dependencyManifest,
                outputManifest,
                nativeModule,
                repository,
                fingerprint,
                configuration,
                cancellationToken)
            .ConfigureAwait(false))
        {
            Console.WriteLine($"{descriptor.DisplayName} Windows bindings are up to date.");
            return;
        }

        InvalidateStamp(stamp);

        Directory.CreateDirectory(output);
        EnsureExecutionDiskBoundary(output, $"{descriptor.DisplayName} generation");
        ResetGeneratedSourceDirectories(output);
        await GenerateProviderAsync(descriptor, output, vcpkg, cancellationToken)
            .ConfigureAwait(false);
        ResetNativeBuildDirectory(output);
        EnsureExecutionDiskBoundary(output, $"{descriptor.DisplayName} native compilation");
        var compiler = await BuildNativeAsync(
                descriptor,
                repository,
                output,
                vcpkg,
                configuration,
                cancellationToken)
            .ConfigureAwait(false);

        EnsureNonemptyFile(nativeLibrary, $"The {descriptor.DisplayName} native library was not produced");
        await nativeDependencies.StageAsync(
                nativeModule,
                compiler,
                nativeLibrary,
                dependencyDirectory,
                Path.Combine(vcpkg, "installed", "x64-windows", "bin"),
                output,
                repository,
                cancellationToken)
            .ConfigureAwait(false);
        StageNotices(descriptor, output, vcpkg);
        WriteManagedManifestIfRequired(descriptor, output, managedManifest);
        if (descriptor.WriteOutputManifest)
        {
            await WriteOutputManifestAsync(descriptor, output, outputManifest, configuration, cancellationToken)
                .ConfigureAwait(false);
        }

        EnsureManagedOutputs(descriptor, output, managedManifest);
        ReclaimNativeBuildIntermediates(descriptor, output);
        var outputFingerprint = await ComputeOutputFingerprintAsync(descriptor, output, configuration, cancellationToken)
            .ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(stamp)!);
        await WriteJsonAtomicallyAsync(
                stamp,
                new GenerationStamp(fingerprint, outputFingerprint),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void EnsureExecutionDiskBoundary(string output, string stage)
    {
        if (operations is null)
        {
            EnsureDiskBoundary(output, stage);
            return;
        }

        operations.EnsureDiskBoundary(output, stage);
    }

    private Task GenerateProviderAsync(
        ProviderDescriptor descriptor,
        string output,
        string vcpkg,
        CancellationToken cancellationToken)
    {
        return operations is null
            ? ProviderGenerators.GenerateAsync(descriptor.Id, new(output), new(vcpkg), cancellationToken)
            : operations.GenerateAsync(descriptor, output, vcpkg, cancellationToken);
    }

    private Task<string> BuildNativeAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        if (operations is not null)
        {
            return operations.BuildNativeAsync(
                descriptor,
                repository,
                output,
                vcpkg,
                configuration,
                cancellationToken);
        }

        return descriptor.NativeBuild is NativeBuildKind.Occt
            ? BuildOcctNativeAsync(repository, output, vcpkg, configuration, cancellationToken)
            : BuildVisualStudioNativeAsync(
                descriptor,
                repository,
                output,
                vcpkg,
                configuration,
                cancellationToken);
    }

    internal async Task<bool> IsCompleteAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        var nativeBuild = Path.Combine(output, "native-build");
        var inputFingerprint = await ComputeInputFingerprintAsync(
                descriptor,
                repository,
                vcpkg,
                cancellationToken)
            .ConfigureAwait(false);
        return await IsCompleteAsync(
                descriptor,
                output,
                Path.Combine(nativeBuild, configuration, descriptor.NativeLibraryName),
                Path.Combine(nativeBuild, configuration, "generation.stamp"),
                Path.Combine(nativeBuild, configuration, "managed-files.txt"),
                Path.Combine(output, "native-dependencies"),
                Path.Combine(output, "native-dependencies.json"),
                Path.Combine(output, "output-manifest.json"),
                Path.Combine(repository, "Build", "NativeDependencyClosure.psm1"),
                repository,
                inputFingerprint,
                configuration,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> IsCompleteAsync(
        ProviderDescriptor descriptor,
        string output,
        string nativeLibrary,
        string stamp,
        string managedManifest,
        string dependencyDirectory,
        string dependencyManifest,
        string outputManifest,
        string nativeModule,
        string repository,
        string inputFingerprint,
        string configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(stamp)
                || !File.Exists(nativeLibrary)
                || new FileInfo(nativeLibrary).Length is 0
                || !NoticesAreComplete(descriptor, output)
                || !ManagedOutputsAreComplete(descriptor, output, managedManifest)
                || (descriptor.WriteOutputManifest
                    && !await OutputManifestIsCompleteAsync(
                            descriptor,
                            output,
                            outputManifest,
                            configuration,
                            cancellationToken)
                        .ConfigureAwait(false))
                || !await nativeDependencies.IsCompleteAsync(
                        nativeModule,
                        nativeLibrary,
                        dependencyDirectory,
                        dependencyManifest,
                        repository,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return false;
            }

            var state = JsonSerializer.Deserialize<GenerationStamp>(
                await File.ReadAllTextAsync(stamp, cancellationToken).ConfigureAwait(false));
            if (state is null || !string.Equals(state.InputFingerprint, inputFingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            var actualOutputFingerprint = await ComputeOutputFingerprintAsync(
                    descriptor,
                    output,
                    configuration,
                    cancellationToken)
                .ConfigureAwait(false);
            return string.Equals(state.OutputFingerprint, actualOutputFingerprint, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<string> BuildOcctNativeAsync(
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        var toolchain = FindOcctToolchain();
        var environment = WithCompilerOption(
            await ReadCompilerEnvironmentAsync(toolchain.EnvironmentScript, repository, cancellationToken)
                .ConfigureAwait(false),
            "/bigobj");
        var configure = await processes.RunAsync(
                "cmake",
                [
                    "--fresh", "-G", "Ninja Multi-Config", "-Wno-unused-cli",
                    "-S", Path.Combine(output, "cpp"),
                    "-B", Path.Combine(output, "native-build"),
                    $"-DCMAKE_MAKE_PROGRAM={toolchain.Ninja}",
                    $"-DCMAKE_CXX_COMPILER={toolchain.Compiler}",
                    $"-DCMAKE_TOOLCHAIN_FILE={Path.Combine(vcpkg, "scripts", "buildsystems", "vcpkg.cmake")}",
                    "-DVCPKG_TARGET_TRIPLET=x64-windows",
                    "-DVCPKG_APPLOCAL_DEPS=OFF",
                ],
                repository,
                environment,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
        WriteProcessOutput(configure);
        if (configure.CombinedOutput.Contains("cannot be safely placed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The native object paths exceed the compiler budget. Use a shorter output root before compiling.");
        }

        var build = await processes.RunAsync(
                "cmake",
                ["--build", Path.Combine(output, "native-build"), "--config", configuration, "--parallel", "1",],
                repository,
                environment,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
        WriteProcessOutput(build);
        return toolchain.Compiler;
    }

    private async Task<string> BuildVisualStudioNativeAsync(
        ProviderDescriptor descriptor,
        string repository,
        string output,
        string vcpkg,
        string configuration,
        CancellationToken cancellationToken)
    {
        var configureArguments = new List<string>
        {
            "--fresh", "-G", "Visual Studio 18 2026", "-A", "x64",
            "-S", Path.Combine(output, "cpp"),
            "-B", Path.Combine(output, "native-build"),
            $"-DCMAKE_TOOLCHAIN_FILE={Path.Combine(vcpkg, "scripts", "buildsystems", "vcpkg.cmake")}",
            "-DVCPKG_TARGET_TRIPLET=x64-windows",
            "-DVCPKG_APPLOCAL_DEPS=OFF",
        };
        if (descriptor.ToolchainProfile is ToolchainProfileKind.Cgal)
        {
            configureArguments.Add($"-DCMAKE_PROJECT_INCLUDE={Path.Combine(repository, "Build", "CgalCompilerIdentity.cmake")}");
        }

        var configure = await processes.RunAsync(
                "cmake",
                configureArguments,
                repository,
                null,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
        WriteProcessOutput(configure);
        var build = await processes.RunAsync(
                "cmake",
                ["--build", Path.Combine(output, "native-build"), "--config", configuration, "--parallel", "1",],
                repository,
                null,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
        WriteProcessOutput(build);

        var identityPath = Path.Combine(output, "native-build", "compiler-identity.txt");
        EnsureNonemptyFile(identityPath, "CMake did not record its selected compiler identity");
        var parts = (await File.ReadAllTextAsync(identityPath, cancellationToken).ConfigureAwait(false))
            .Trim()
            .Split('|', 3);
        if (parts.Length is not 3 || !File.Exists(parts[2]))
        {
            throw new InvalidOperationException($"CMake selected an invalid compiler identity '{string.Join('|', parts)}'.");
        }

        var expected = await ReadExpectedToolchainAsync(descriptor, output, cancellationToken).ConfigureAwait(false);
        EnsureCompilerMatchesLockedProfile(parts[0], parts[1], parts[2], expected.Msvc);

        var cmake = await processes.RunAsync(
                "cmake",
                ["--version",],
                repository,
                null,
                throwOnError: true,
                cancellationToken)
            .ConfigureAwait(false);
        var match = CmakeVersionRegex().Match(cmake.StandardOutput);
        if (!match.Success || !string.Equals(match.Groups[1].Value, expected.Cmake, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The native build used CMake '{cmake.StandardOutput.Trim()}', which does not match the locked profile.");
        }

        await WriteJsonAtomicallyAsync(
                Path.Combine(output, "build-toolchain.json"),
                new BuildToolchain(parts[0], parts[1], Path.GetFullPath(parts[2]), match.Groups[1].Value, GetToolsetVersion(parts[2])),
                cancellationToken)
            .ConfigureAwait(false);
        return parts[2];
    }

    private static async Task<ToolchainExpectation> ReadExpectedToolchainAsync(
        ProviderDescriptor descriptor,
        string output,
        CancellationToken cancellationToken)
    {
        var path = descriptor.ToolchainProfile is ToolchainProfileKind.Cgal
            ? Path.Combine(output, "csharp", "toolchain-inventory.json")
            : Path.Combine(output, "csharp", "profile-manifest.json");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        return descriptor.ToolchainProfile switch
        {
            ToolchainProfileKind.Cgal or ToolchainProfileKind.Manifold => new(
                root.GetProperty("Msvc").GetString()!,
                root.GetProperty("CMake").GetString()!),
            ToolchainProfileKind.Fcl => new(
                root.GetProperty("Versions").GetProperty("Msvc").GetString()!,
                root.GetProperty("Versions").GetProperty("CMake").GetString()!),
            _ => throw new InvalidOperationException($"{descriptor.DisplayName} has no locked toolchain profile."),
        };
    }

    internal async Task<IReadOnlyDictionary<string, string>> ReadCompilerEnvironmentAsync(
        string environmentScript,
        string repository,
        CancellationToken cancellationToken)
    {
        var commandInterpreter = Environment.GetEnvironmentVariable("COMSPEC");
        if (string.IsNullOrWhiteSpace(commandInterpreter))
        {
            throw new InvalidOperationException("COMSPEC is required to initialize the Visual Studio compiler environment.");
        }

        var commandFile = Path.Combine(
            Path.GetTempPath(),
            $"tedtoolkit-compiler-environment-{Guid.NewGuid():N}.cmd");
        ProcessResult result;
        try
        {
            await File.WriteAllTextAsync(
                    commandFile,
                    $"@call \"{environmentScript}\" >nul{Environment.NewLine}"
                    + $"@if errorlevel 1 exit /b %errorlevel%{Environment.NewLine}"
                    + $"@set{Environment.NewLine}",
                    cancellationToken)
                .ConfigureAwait(false);
            result = await processes.RunAsync(
                    commandInterpreter,
                    ["/d", "/c", commandFile,],
                    repository,
                    null,
                    throwOnError: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            File.Delete(commandFile);
        }

        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                environment[line[..separator]] = line[(separator + 1)..];
            }
        }

        return environment;
    }

    private static OcctToolchain FindOcctToolchain()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Microsoft Visual Studio");
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException(
                "Visual Studio with the MSVC C++ and CMake components is required to generate Windows bindings.");
        }

        foreach (var installation in Directory.EnumerateDirectories(root)
                     .SelectMany(Directory.EnumerateDirectories)
                     .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var ninja = Path.Combine(
                installation,
                "Common7", "IDE", "CommonExtensions", "Microsoft", "CMake", "Ninja", "ninja.exe");
            var environment = Path.Combine(installation, "VC", "Auxiliary", "Build", "vcvars64.bat");
            var msvcRoot = Path.Combine(installation, "VC", "Tools", "MSVC");
            var compiler = Directory.Exists(msvcRoot)
                ? Directory.EnumerateDirectories(msvcRoot)
                    .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(static path => Path.Combine(path, "bin", "Hostx64", "x64", "cl.exe"))
                    .FirstOrDefault(File.Exists)
                : null;
            if (File.Exists(ninja) && File.Exists(environment) && compiler is not null)
            {
                return new(ninja, compiler, environment);
            }
        }

        throw new InvalidOperationException(
            "Visual Studio with the MSVC C++ and CMake components is required to generate Windows bindings.");
    }

    internal static async Task<string> ComputeInputFingerprintAsync(
        ProviderDescriptor descriptor,
        string repository,
        string vcpkg,
        CancellationToken cancellationToken)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativeRoot in descriptor.RepositoryInputRoots)
        {
            var root = Path.Combine(repository, NormalizeRelativePath(relativeRoot));
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!HasBuildOutputSegment(file))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }
        }

        foreach (var relativeFile in descriptor.RepositoryInputFiles)
        {
            var path = Path.Combine(repository, NormalizeRelativePath(relativeFile));
            if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        var nativeClosureModule = Path.Combine(repository, "Build", "NativeDependencyClosure.psm1");
        if (File.Exists(nativeClosureModule))
        {
            files.Add(Path.GetFullPath(nativeClosureModule));
        }

        foreach (var relativeRoot in descriptor.VcpkgInputRoots)
        {
            AddInputTree(files, Path.Combine(vcpkg, NormalizeRelativePath(relativeRoot)));
        }

        foreach (var pattern in descriptor.VcpkgInputPatterns)
        {
            var root = Path.Combine(vcpkg, NormalizeRelativePath(pattern.RelativeRoot));
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, pattern.SearchPattern, SearchOption.TopDirectoryOnly))
            {
                files.Add(Path.GetFullPath(file));
            }
        }

        var vcpkgStatus = Path.Combine(vcpkg, "installed", "vcpkg", "status");
        if (File.Exists(vcpkgStatus))
        {
            files.Add(vcpkgStatus);
        }

        foreach (var notice in descriptor.Notices.Values)
        {
            var path = Path.Combine(vcpkg, NormalizeRelativePath(notice));
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        var lines = new List<string>();
        foreach (var file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{file}|{await HashFileAsync(file, cancellationToken).ConfigureAwait(false)}");
        }

        if (descriptor.IncludeVcpkgPathInFingerprint)
        {
            lines.Add(Path.GetFullPath(vcpkg));
        }

        return HashText(string.Join('\n', lines));
    }

    private static void AddInputTree(HashSet<string> files, string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            files.Add(Path.GetFullPath(file));
        }
    }

    internal static async Task<string> ComputeOutputFingerprintAsync(
        ProviderDescriptor descriptor,
        string output,
        string configuration,
        CancellationToken cancellationToken)
    {
        var files = EnumerateManifestFiles(descriptor, output, configuration, includeManifests: true);
        var lines = new List<string>();
        foreach (var file in files)
        {
            lines.Add(
                $"{Path.GetRelativePath(output, file).Replace('\\', '/')}|{new FileInfo(file).Length}|"
                + await HashFileAsync(file, cancellationToken).ConfigureAwait(false));
        }

        return HashText(string.Join('\n', lines));
    }

    private static IEnumerable<string> EnumerateManifestFiles(
        ProviderDescriptor descriptor,
        string output,
        string configuration,
        bool includeManifests)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in new[] { "csharp", "cpp", "native-dependencies", "third-party-notices", })
        {
            var path = Path.Combine(output, directory);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }
        }

        foreach (var relativePath in new[]
                 {
                     "generation-result.json",
                     "build-toolchain.json",
                     "native-dependencies.json",
                     Path.Combine("native-build", configuration, descriptor.NativeLibraryName),
                 })
        {
            var path = Path.Combine(output, relativePath);
            if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        if (includeManifests)
        {
            foreach (var relativePath in new[]
                     {
                         "output-manifest.json",
                         Path.Combine("native-build", configuration, "managed-files.txt"),
                     })
            {
                var path = Path.Combine(output, relativePath);
                if (File.Exists(path))
                {
                    files.Add(Path.GetFullPath(path));
                }
            }
        }

        return files.Order(StringComparer.OrdinalIgnoreCase);
    }

    internal static async Task WriteOutputManifestAsync(
        ProviderDescriptor descriptor,
        string output,
        string manifest,
        string configuration,
        CancellationToken cancellationToken)
    {
        var entries = new List<OutputManifestEntry>();
        foreach (var file in EnumerateManifestFiles(descriptor, output, configuration, includeManifests: false))
        {
            entries.Add(new(
                Path.GetRelativePath(output, file).Replace('\\', '/'),
                new FileInfo(file).Length,
                await HashFileAsync(file, cancellationToken).ConfigureAwait(false)));
        }

        if (entries.Count is 0)
        {
            throw new InvalidOperationException($"Cannot record an empty {descriptor.DisplayName} output manifest.");
        }

        await WriteJsonAtomicallyAsync(manifest, new OutputManifest(entries), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> OutputManifestIsCompleteAsync(
        ProviderDescriptor descriptor,
        string output,
        string manifest,
        string configuration,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifest))
        {
            return false;
        }

        var recorded = JsonSerializer.Deserialize<OutputManifest>(
            await File.ReadAllTextAsync(manifest, cancellationToken).ConfigureAwait(false));
        if (recorded?.Files is null || recorded.Files.Count is 0)
        {
            return false;
        }

        var actual = EnumerateManifestFiles(descriptor, output, configuration, includeManifests: false)
            .ToDictionary(
                file => Path.GetRelativePath(output, file).Replace('\\', '/'),
                StringComparer.Ordinal);
        if (recorded.Files.Count != actual.Count)
        {
            return false;
        }

        foreach (var entry in recorded.Files)
        {
            if (!actual.TryGetValue(entry.Path, out var file)
                || new FileInfo(file).Length != entry.Length
                || !string.Equals(
                    await HashFileAsync(file, cancellationToken).ConfigureAwait(false),
                    entry.Hash,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static void WriteManagedManifestIfRequired(
        ProviderDescriptor descriptor,
        string output,
        string managedManifest)
    {
        if (!descriptor.WriteManagedManifest)
        {
            return;
        }

        var managedFiles = GetManagedFiles(output);
        if (managedFiles.Count is 0 || managedFiles.Any(static file => file.Length is 0))
        {
            throw new InvalidOperationException(
                $"The {descriptor.DisplayName} generator did not produce a complete managed source set.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(managedManifest)!);
        File.WriteAllLines(
            managedManifest,
            managedFiles.Select(file => Path.GetRelativePath(output, file.FullName)).Order(StringComparer.Ordinal));
    }

    private static void EnsureManagedOutputs(
        ProviderDescriptor descriptor,
        string output,
        string managedManifest)
    {
        if (!ManagedOutputsAreComplete(descriptor, output, managedManifest))
        {
            throw new InvalidOperationException(
                $"The {descriptor.DisplayName} generator did not produce a complete nonempty managed source set.");
        }
    }

    private static bool ManagedOutputsAreComplete(
        ProviderDescriptor descriptor,
        string output,
        string managedManifest)
    {
        var actual = GetManagedFiles(output);
        if (actual.Count is 0 || actual.Any(static file => file.Length is 0))
        {
            return false;
        }

        if (descriptor.RequiredManagedFile is not null
            && !actual.Any(file => string.Equals(file.Name, descriptor.RequiredManagedFile, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!descriptor.WriteManagedManifest || !File.Exists(managedManifest))
        {
            return !descriptor.WriteManagedManifest;
        }

        var expected = File.ReadAllLines(managedManifest)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualPaths = actual.Select(file => Path.GetRelativePath(output, file.FullName))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return expected.Length > 0 && expected.SequenceEqual(actualPaths, StringComparer.Ordinal);
    }

    private static IReadOnlyList<FileInfo> GetManagedFiles(string output)
    {
        var path = Path.Combine(output, "csharp");
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(static file => new FileInfo(file))
                .OrderBy(static file => file.FullName, StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static void StageNotices(ProviderDescriptor descriptor, string output, string vcpkg)
    {
        if (descriptor.Notices.Count is 0)
        {
            return;
        }

        var destination = Path.Combine(output, "third-party-notices");
        EnsureChildPath(output, destination);
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(destination);
        foreach (var notice in descriptor.Notices)
        {
            var source = Path.Combine(vcpkg, NormalizeRelativePath(notice.Value));
            EnsureNonemptyFile(source, "Required notice input is missing");
            File.Copy(source, Path.Combine(destination, notice.Key));
        }
    }

    private static bool NoticesAreComplete(ProviderDescriptor descriptor, string output)
    {
        return descriptor.Notices.Keys.All(name =>
        {
            var path = Path.Combine(output, "third-party-notices", name);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        });
    }

    private static void EnsureDiskBoundary(string path, string phase)
    {
        var resolved = Path.GetFullPath(path);
        var root = Path.GetPathRoot(resolved)
            ?? throw new InvalidOperationException($"Cannot resolve the drive for '{resolved}'.");
        var freeBytes = new DriveInfo(root).AvailableFreeSpace;
        var scratchRoot = Environment.GetEnvironmentVariable("TEDTOOLKIT_NATIVE_SCRATCH_ROOT");
        var scratchBytes = !string.IsNullOrWhiteSpace(scratchRoot) && Directory.Exists(scratchRoot)
            ? Directory.EnumerateFiles(scratchRoot, "*", SearchOption.AllDirectories)
                .Sum(static file => new FileInfo(file).Length)
            : 0L;
        EnsureDiskBoundary(freeBytes, scratchBytes, phase);
    }

    internal static void EnsureDiskBoundary(long freeBytes, long scratchBytes, string phase)
    {
        const long freeFloorBytes = 25L * 1024 * 1024 * 1024;
        const long scratchBudgetBytes = 12L * 1024 * 1024 * 1024;
        if (freeBytes < freeFloorBytes || scratchBytes > scratchBudgetBytes)
        {
            throw new InvalidOperationException(
                $"Disk guard stopped phase '{phase}': free={freeBytes / (double)(1024L * 1024 * 1024):N2} GiB "
                + $"(floor=25.00 GiB), scratch={scratchBytes / (double)(1024L * 1024 * 1024):N2} GiB "
                + "(budget=12.00 GiB).");
        }
    }

    internal static void EnsureOwnedOutputRoot(string repository, string output)
    {
        var allowed = new[] { "output", "out", }
            .Select(name => Path.GetFullPath(Path.Combine(repository, name)) + Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(output);
        if (!allowed.Any(root => candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("OutputRoot must remain under the repository output or evidence root.");
        }
    }

    private static void EnsureChildPath(string root, string path)
    {
        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var resolvedPath = Path.GetFullPath(path);
        if (!resolvedPath.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{resolvedPath}' must remain beneath '{resolvedRoot}'.");
        }
    }

    private static void EnsureNonemptyFile(string path, string message)
    {
        if (!File.Exists(path) || new FileInfo(path).Length is 0)
        {
            throw new InvalidOperationException($"{message}: '{path}'.");
        }
    }

    private static bool HasBuildOutputSegment(string path)
    {
        var segments = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string GetToolsetVersion(string compiler)
    {
        var match = ToolsetPathRegex().Match(Path.GetFullPath(compiler));
        if (!match.Success)
        {
            throw new InvalidOperationException($"CMake selected an unsupported compiler path '{compiler}'.");
        }

        return match.Groups[1].Value;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static string GetMutexName(ProviderDescriptor descriptor, string output)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var normalizedOutput = Path.GetFullPath(output).ToUpperInvariant();
        return $"Local\\TedToolkit.CppBindings.Windows.Generation.Tool.{HashText(normalizedOutput)}";
    }

    internal static void InvalidateStamp(string stamp)
    {
        if (File.Exists(stamp))
        {
            File.Delete(stamp);
        }
    }

    internal static void ResetNativeBuildDirectory(string output)
    {
        var nativeBuild = Path.Combine(output, "native-build");
        EnsureChildPath(output, nativeBuild);
        if (Directory.Exists(nativeBuild))
        {
            Directory.Delete(nativeBuild, recursive: true);
        }
    }

    internal static void ResetGeneratedSourceDirectories(string output)
    {
        foreach (var name in new[] { "csharp", "cpp", })
        {
            var path = Path.Combine(output, name);
            EnsureChildPath(output, path);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    internal static IReadOnlyDictionary<string, string> WithCompilerOption(
        IReadOnlyDictionary<string, string> environment,
        string option)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(option);
        var result = new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase);
        result.TryGetValue("CL", out var existing);
        if (string.IsNullOrWhiteSpace(existing))
        {
            result["CL"] = option;
        }
        else if (!existing.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                     .Contains(option, StringComparer.OrdinalIgnoreCase))
        {
            result["CL"] = $"{existing} {option}";
        }

        return result;
    }

    internal static void EnsureCompilerMatchesLockedProfile(
        string compilerId,
        string compilerVersion,
        string compilerPath,
        string expectedMsvc)
    {
        if (!string.Equals(compilerId, "MSVC", StringComparison.Ordinal)
            || !string.Equals(compilerVersion, $"{expectedMsvc}.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"CMake selected compiler '{compilerId}|{compilerVersion}|{compilerPath}', which does not match the locked profile.");
        }
    }

    internal static void ReclaimNativeBuildIntermediates(ProviderDescriptor descriptor, string output)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.NativeBuild is not NativeBuildKind.Occt)
        {
            return;
        }

        var targetObjects = Path.Combine(output, "native-build", "CMakeFiles", "ted_toolkit_occt.dir");
        EnsureChildPath(output, targetObjects);
        if (Directory.Exists(targetObjects))
        {
            Directory.Delete(targetObjects, recursive: true);
        }
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }

    private static void WriteProcessOutput(ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.Write(result.StandardOutput);
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Console.Error.Write(result.StandardError);
        }
    }

    [GeneratedRegex(@"^cmake version (.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CmakeVersionRegex();

    [GeneratedRegex(@"[\\/]VC[\\/]Tools[\\/]MSVC[\\/]([^\\/]+)[\\/]bin[\\/]Hostx64[\\/]x64[\\/]cl\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ToolsetPathRegex();

    private sealed record GenerationStamp(string InputFingerprint, string OutputFingerprint);

    private sealed record OutputManifest(IReadOnlyList<OutputManifestEntry> Files);

    private sealed record OutputManifestEntry(string Path, long Length, string Hash);

    private sealed record ToolchainExpectation(string Msvc, string Cmake);

    private sealed record OcctToolchain(string Ninja, string Compiler, string EnvironmentScript);

    private sealed record BuildToolchain(
        string CompilerId,
        string CompilerVersion,
        string CompilerPath,
        string CMake,
        string ToolsetVersion);
}
