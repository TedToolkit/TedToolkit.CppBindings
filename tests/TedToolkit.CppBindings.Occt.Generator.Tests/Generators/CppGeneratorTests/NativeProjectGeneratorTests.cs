// -----------------------------------------------------------------------
// <copyright file="NativeProjectGeneratorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator;
using TedToolkit.CppBindings.Occt.Generator.Services;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CppGeneratorTests;

/// <summary>
/// Verifies the generated native build topology.
/// </summary>
internal sealed class NativeProjectGeneratorTests
{
    /// <summary>
    /// Verifies that native compilation is serialized while sources are batched into bounded unity units.
    /// </summary>
    /// <returns>A task that completes when the generated CMake assertions finish.</returns>
    [Test]
    public async Task Should_generate_single_worker_bounded_unity_build_Async()
    {
        var sources = Enumerable.Range(0, 33).Select(static index => $"Flat_{index:D2}.cpp").ToList();
        sources.Add("Units.cpp");
        sources.Add("Units_Dimensions.cpp");
        var definition = OcctGenerationProvider.CreateNativeProjectDefinition(new()
        {
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
            DeclOptions = [],
            NativeLibraryBaseName = "fixture",
            CppVersion = 17,
        });
        var project = BindingCMakeProjectEmitter.Render(definition, sources);
        var exactProject = BindingCMakeProjectEmitter.Render(
            OcctGenerationProvider.CreateNativeProjectDefinition(new()
            {
                CSharpFolder = new(Path.GetTempPath()),
                CppFolder = new(Path.GetTempPath()),
                DeclOptions = [],
                NativeLibraryBaseName = "fixture",
                CppVersion = 17,
            }, new(8, 0, 1)),
            sources);

        await Assert.That(project).Contains("PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/MP1>");
        await Assert.That(project).Contains("find_package(OpenCASCADE CONFIG REQUIRED)");
        await Assert.That(exactProject).Contains("find_package(OpenCASCADE 8.0.1 EXACT CONFIG REQUIRED)");
        await Assert.That(project).Contains("UNITY_BUILD ON");
        await Assert.That(project).Contains("UNITY_BUILD_MODE GROUP");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Flat_depth_1_batch_1\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_0_batch_0\"");
        await Assert.That(project).Contains("PROPERTIES UNITY_GROUP \"Units_depth_1_batch_0\"");
        await Assert.That(project).DoesNotContain("/MP8");
        await Assert.That(project.IndexOf("Units.cpp", StringComparison.Ordinal))
            .IsLessThan(project.IndexOf("Units_Dimensions.cpp", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies a requested OCCT version must match the installed package exactly.
    /// </summary>
    /// <returns>A task that completes when the rejection assertion finishes.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_reject_a_native_version_that_is_not_installed_Async()
    {
        var originalRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            var metadata = Directory.CreateDirectory(Path.Combine(root.FullName, "installed", "vcpkg"));
            await File.WriteAllTextAsync(
                    Path.Combine(metadata.FullName, "status"),
                    "Package: opencascade\nVersion: 8.0.1\nArchitecture: x64-windows\nStatus: install ok installed\n\n")
                .ConfigureAwait(false);
            Environment.SetEnvironmentVariable("VCPKG_ROOT", root.FullName);
            var options = new OcctGenerationOptions()
            {
                CSharpFolder = root.CreateSubdirectory("managed"),
                CppFolder = root.CreateSubdirectory("native"),
                DeclOptions = [],
                Triplet = "x64-windows",
                NativeLibraryVersion = new(8, 0, 0),
            };
            var provider = new OcctGenerationProvider(options);
            await File.WriteAllTextAsync(Path.Combine(options.CSharpFolder.FullName, "stale.txt"), "managed")
                .ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(options.CppFolder.FullName, "stale.txt"), "native")
                .ConfigureAwait(false);

            InvalidOperationException? failure = null;
            try
            {
                _ = await provider.CreatePlanAsync(options, CancellationToken.None).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Message).Contains("does not match installed opencascade");
            await Assert.That(await File.ReadAllTextAsync(
                    Path.Combine(options.CSharpFolder.FullName, "stale.txt")).ConfigureAwait(false))
                .IsEqualTo("managed");
            await Assert.That(await File.ReadAllTextAsync(
                    Path.Combine(options.CppFolder.FullName, "stale.txt")).ConfigureAwait(false))
                .IsEqualTo("native");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VCPKG_ROOT", originalRoot);
            root.Delete(true);
        }
    }
}