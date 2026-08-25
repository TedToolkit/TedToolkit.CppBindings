// -----------------------------------------------------------------------
// <copyright file="GenerateAbiProjectAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using ModularPipelines.Context;

using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;

namespace TedToolkit.Occt.Generator.Tests.Modules.GenerateCppModuleTests;

/// <summary>
/// Verifies production ABI project materialization.
/// </summary>
internal sealed class GenerateAbiProjectAsyncTests
{
    private const string CUSTOM_LIBRARY_BASE_NAME = "product_geometry";

    /// <summary>
    /// Verifies that omitted library-name configuration produces the clean default output basename.
    /// </summary>
    /// <returns>A task that completes when the generated CMake assertion has finished.</returns>
    [Test]
    public async Task Should_materialize_the_clean_default_library_basename_Async()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(outputDirectory, CancellationToken.None)
                .ConfigureAwait(false);

            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);

            await Assert.That(cmake).Contains("OUTPUT_NAME \"ted_toolkit_occt\"");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies that production materialization emits no test-only header.
    /// </summary>
    /// <returns>A task that completes when the generated header inventory assertion has finished.</returns>
    [Test]
    public async Task Should_materialize_only_the_canonical_public_header_Async()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(outputDirectory, CancellationToken.None)
                .ConfigureAwait(false);

            var headers = outputDirectory
                .EnumerateFiles("*.h", SearchOption.AllDirectories)
                .Select(file => file.Name)
                .ToArray();

            await Assert.That(headers).IsEquivalentTo(["ted_toolkit_occt_v1.h",]);
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies that explicit library-name configuration changes only the output basename.
    /// </summary>
    /// <returns>A task that completes when the generated CMake assertions have finished.</returns>
    [Test]
    public async Task Should_materialize_a_configured_library_basename_Async()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(
                    outputDirectory,
                    CUSTOM_LIBRARY_BASE_NAME,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);

            await Assert.That(cmake).Contains($"OUTPUT_NAME \"{CUSTOM_LIBRARY_BASE_NAME}\"");
            await Assert.That(cmake).Contains("add_library(ted_toolkit_occt_abi_v1 SHARED");
            await Assert.That(cmake).DoesNotContain("@TED_OCCT_V1_NATIVE_LIBRARY_BASENAME@");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies that the public generation option reaches native project materialization.
    /// </summary>
    /// <returns>A task that completes when module execution has finished.</returns>
    [Test]
    public async Task Should_apply_the_configured_library_basename_during_module_execution_Async()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var options = new GenerationOptions()
            {
                DeclOptions = [],
                CSharpFolder = new(Path.Combine(outputDirectory.FullName, "csharp")),
                CppFolder = outputDirectory,
                NativeLibraryBaseName = CUSTOM_LIBRARY_BASE_NAME,
            };
            var module = new GenerateCppModule(Microsoft.Extensions.Options.Options.Create(options));
            var executeAsyncMethod = typeof(GenerateCppModule).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            await Assert.That(executeAsyncMethod).IsNotNull();
            var execution = (Task<bool>)executeAsyncMethod!
                .Invoke(module, [Mock.Of<IModuleContext>(), CancellationToken.None,])!;
            var result = await execution.ConfigureAwait(false);
            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);

            await Assert.That(result).IsTrue();
            await Assert.That(cmake).Contains($"OUTPUT_NAME \"{CUSTOM_LIBRARY_BASE_NAME}\"");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies that an invalid native library basename is rejected before existing output changes.
    /// </summary>
    /// <param name="invalidBaseName">The invalid basename.</param>
    /// <returns>A task that completes when the atomic rejection assertions have finished.</returns>
    [Test]
    [Arguments("")]
    [Arguments("folder/product")]
    [Arguments(@"folder\product")]
    [Arguments("libproduct")]
    [Arguments("product.dll")]
    [Arguments("product.so")]
    [Arguments("product.dylib")]
    [Arguments("CON")]
    [Arguments("COM1.trace")]
    [Arguments("9product")]
    [Arguments("product-")]
    [Arguments("product name")]
    public async Task Should_reject_an_invalid_library_basename_before_writing_output_Async(string invalidBaseName)
    {
        await VerifyInvalidBaseNameIsAtomicAsync(invalidBaseName).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that an over-length native library basename is rejected before existing output changes.
    /// </summary>
    /// <returns>A task that completes when the atomic rejection assertions have finished.</returns>
    [Test]
    public async Task Should_reject_an_overlength_library_basename_before_writing_output_Async()
    {
        await VerifyInvalidBaseNameIsAtomicAsync(new string('a', 241)).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies that the portable basename length boundary is accepted.
    /// </summary>
    /// <returns>A task that completes when the boundary materialization assertion has finished.</returns>
    [Test]
    public async Task Should_accept_a_240_character_library_basename_Async()
    {
        var outputDirectory = CreateOutputDirectory();
        var baseName = new string('a', 240);
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(outputDirectory, baseName, CancellationToken.None)
                .ConfigureAwait(false);

            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);
            await Assert.That(cmake).Contains($"OUTPUT_NAME \"{baseName}\"");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies representative portable basenames are accepted.
    /// </summary>
    /// <param name="baseName">The valid basename.</param>
    /// <returns>A task that completes when the materialization assertion has finished.</returns>
    [Test]
    [Arguments("a")]
    [Arguments("product")]
    public async Task Should_accept_a_portable_library_basename_Async(string baseName)
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(outputDirectory, baseName, CancellationToken.None)
                .ConfigureAwait(false);

            var cmake = await File.ReadAllTextAsync(Path.Combine(outputDirectory.FullName, "CMakeLists.txt"))
                .ConfigureAwait(false);
            await Assert.That(cmake).Contains($"OUTPUT_NAME \"{baseName}\"");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    /// <summary>
    /// Verifies that a custom-named production build exports the ABI but no test-only hooks.
    /// </summary>
    /// <returns>A task that completes when the native boundary assertions have finished.</returns>
    /// <exception cref="PlatformNotSupportedException">The test is not running on Windows.</exception>
    /// <exception cref="InvalidOperationException">The native toolchain is unavailable or fails.</exception>
    [Test]
    [NotInParallel("native-build-toolchain")]
    public async Task Should_build_a_custom_named_production_library_without_test_exports_Async()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The approved native verification baseline is Windows x64.");
        }

        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(vcpkgRoot))
        {
            throw new InvalidOperationException("VCPKG_ROOT is required for the native boundary proof.");
        }

        var rootDirectory = CreateOutputDirectory();
        var sourceDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "source"));
        var buildDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "build"));
        var ninjaPath = ResolveNinjaPath()
                        ?? throw new InvalidOperationException("Ninja is required for the native boundary proof.");
        try
        {
            _ = await GenerateCppModule.GenerateAbiProjectAsync(
                    sourceDirectory,
                    CUSTOM_LIBRARY_BASE_NAME,
                    CancellationToken.None)
                .ConfigureAwait(false);

            await RunProcessAsync(
                    "cmake",
                    rootDirectory,
                    "-S",
                    sourceDirectory.FullName,
                    "-B",
                    buildDirectory.FullName,
                    "-G",
                    "Ninja",
                    $"-DCMAKE_MAKE_PROGRAM={ninjaPath}",
                    "-DCMAKE_BUILD_TYPE=Release",
                    "-DCMAKE_CXX_COMPILER=clang-cl",
                    $"-DCMAKE_TOOLCHAIN_FILE={Path.Combine(vcpkgRoot, "scripts", "buildsystems", "vcpkg.cmake")}",
                    "-DVCPKG_TARGET_TRIPLET=x64-windows",
                    "-DVCPKG_APPLOCAL_DEPS=OFF")
                .ConfigureAwait(false);
            await RunProcessAsync("cmake", rootDirectory, "--build", buildDirectory.FullName)
                .ConfigureAwait(false);

            var library = buildDirectory
                .EnumerateFiles($"{CUSTOM_LIBRARY_BASE_NAME}.dll", SearchOption.AllDirectories)
                .Single();
            CopyNativeDependencies(vcpkgRoot, library.Directory
                ?? throw new InvalidOperationException("The built native library has no parent directory."));
            var handle = NativeLibrary.Load(library.FullName);
            try
            {
                await Assert.That(NativeLibrary.TryGetExport(handle, "ted_occt_v1_abi_version", out _)).IsTrue();
                await Assert.That(NativeLibrary.TryGetExport(handle, "ted_occt_v1_test_layout", out _)).IsFalse();
                await Assert.That(NativeLibrary.TryGetExport(handle, "ted_occt_v1_test_error", out _)).IsFalse();
                await Assert.That(NativeLibrary.TryGetExport(
                        handle,
                        "ted_occt_v1_test_fail_diagnostic_allocation_after",
                        out _))
                    .IsFalse();
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
        }
        finally
        {
            DeleteOutputDirectory(rootDirectory);
        }
    }

    private static async Task VerifyInvalidBaseNameIsAtomicAsync(string invalidBaseName)
    {
        var outputDirectory = CreateOutputDirectory();
        outputDirectory.Create();
        var sentinel = new FileInfo(Path.Combine(outputDirectory.FullName, "sentinel.txt"));
        await File.WriteAllTextAsync(sentinel.FullName, "preserve").ConfigureAwait(false);
        try
        {
            ArgumentException? exception = null;
            try
            {
                _ = await GenerateCppModule.GenerateAbiProjectAsync(
                        outputDirectory,
                        invalidBaseName,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (ArgumentException caught)
            {
                exception = caught;
            }

            await Assert.That(exception).IsNotNull();
            await Assert.That(outputDirectory.EnumerateFileSystemInfos().Select(item => item.Name))
                .IsEquivalentTo([sentinel.Name,]);
            await Assert.That(await File.ReadAllTextAsync(sentinel.FullName).ConfigureAwait(false))
                .IsEqualTo("preserve");
        }
        finally
        {
            DeleteOutputDirectory(outputDirectory);
        }
    }

    private static async Task RunProcessAsync(
        string fileName,
        DirectoryInfo workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process() { StartInfo = startInfo, };
        _ = process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);
        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{fileName} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{error}");
    }

    private static void CopyNativeDependencies(string vcpkgRoot, DirectoryInfo targetDirectory)
    {
        var nativeDirectory = new DirectoryInfo(Path.Combine(vcpkgRoot, "installed", "x64-windows", "bin"));
        foreach (var dependency in nativeDirectory.EnumerateFiles("*.dll"))
        {
            _ = dependency.CopyTo(Path.Combine(targetDirectory.FullName, dependency.Name), true);
        }
    }

    private static string? FindExecutable(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, executableName))
            .FirstOrDefault(File.Exists);
    }

    private static string? ResolveNinjaPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("NINJA_EXE");
        return !string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)
            ? configuredPath
            : FindExecutable("ninja.exe");
    }

    private static DirectoryInfo CreateOutputDirectory()
    {
        return new(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
    }

    private static void DeleteOutputDirectory(DirectoryInfo outputDirectory)
    {
        if (!outputDirectory.Exists)
        {
            return;
        }

        outputDirectory.Delete(true);
    }
}