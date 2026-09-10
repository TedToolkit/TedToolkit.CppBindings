// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using TedToolkit.CppBindings.Manifold.Generator;

namespace TedToolkit.CppBindings.Manifold.Generator.Tests;

/// <summary>
/// Verifies the locked finite Manifold profile and paired source renderer.
/// </summary>
internal sealed class ManifoldGenerationProviderTests
{
    /// <summary>
    /// Verifies that two plans are byte-identical and include the complete finite ABI authority.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_create_byte_identical_complete_plans_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var first = new ManifoldGenerationProvider().CreatePlan(vcpkgRoot);
        var second = new ManifoldGenerationProvider().CreatePlan(vcpkgRoot);

        await Assert.That(first.ProfileId).IsEqualTo("manifold-3.5.2-windows-v2");
        await Assert.That(first.NativeExports.Count).IsEqualTo(9);
        await Assert.That(first.NativeExports).IsEquivalentTo(second.NativeExports);
        await Assert.That(first.ManagedSources.Keys).IsEquivalentTo(second.ManagedSources.Keys);
        await Assert.That(first.NativeSources.Keys).IsEquivalentTo(second.NativeSources.Keys);
        await Assert.That(first.ManagedSources.Keys).Contains("layout-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("ownership-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("source-declaration-inventory.json");
        await Assert.That(first.ManagedSources.Keys).Contains("toolchain-inventory.json");
        foreach (var source in first.ManagedSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.ManagedSources[source.Key]);
        }

        foreach (var source in first.NativeSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.NativeSources[source.Key]);
        }

        var managed = first.ManagedSources["Manifold.Bindings.g.cs"];
        var native = first.NativeSources["ManifoldProfileAdapter.cpp"];
        await Assert.That(managed).Contains("public enum ManifoldOp : sbyte");
        await Assert.That(managed).Contains("public static global::TedToolkit.CppBindings.Owned<Manifold> Create");
        await Assert.That(managed).Contains("ReadOnlySpan<double> vertexCoordinates");
        await Assert.That(managed).Contains("GC.SuppressFinalize(result);");
        await Assert.That(native).Contains("static_assert(sizeof(ManifoldAdapter) == 8");
        await Assert.That(native).Contains("Manifold::Error::Cancelled) == 14");
        await Assert.That(native).Contains("NativeApi_GetFunctionTable");
        await Assert.That(native).Contains("SetError(error, 3, \"std::underflow_error\"");
        await Assert.That(native).Contains("SetError(error, 8, \"std::exception\"");
        await Assert.That(native).DoesNotContain("SetError(error, 9, \"std::exception\"");
    }

    /// <summary>
    /// Verifies that vcpkg is the complete public-header inventory authority.
    /// </summary>
    /// <returns>A task that completes when inventory assertions finish.</returns>
    [Test]
    public async Task Should_resolve_complete_header_inventory_from_vcpkg_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var plan = new ManifoldGenerationProvider().CreatePlan(vcpkgRoot);
        using var document = JsonDocument.Parse(plan.ManagedSources["source-inventory.json"]);
        var actual = document.RootElement.EnumerateArray()
            .Select(static item => (
                Header: item.GetProperty("header").GetString()!,
                Disposition: item.GetProperty("disposition").GetString()!))
            .ToArray();
        var expected = ReadPackageHeaders(vcpkgRoot);

        await Assert.That(actual.Select(static item => item.Header).SequenceEqual(expected, StringComparer.Ordinal))
            .IsTrue();
        await Assert.That(actual.Single(static item => item.Header == "manifold/manifold.h").Disposition)
            .IsEqualTo("profile-root");
        await Assert.That(actual.Single(static item => item.Header == "manifold/math.h").Disposition)
            .IsEqualTo("reachable-dependency");
        await Assert.That(actual.Single(static item => item.Header == "manifold/vec_view.h").Disposition)
            .IsEqualTo("reachable-dependency");
        await Assert.That(actual.Single(static item => item.Header == "manifold/manifoldc.h").Disposition)
            .IsEqualTo("not-reachable-from-finite-profile");
        await Assert.That(typeof(ManifoldGenerationProvider).Assembly.GetManifestResourceNames()
            .Any(static name => name.EndsWith("manifold-3.5.2-windows-v1.json", StringComparison.Ordinal)))
            .IsFalse();
    }

    /// <summary>
    /// Verifies every installed header is classified by the complete include-closure rules.
    /// </summary>
    /// <returns>A task that completes when classification assertions finish.</returns>
    [Test]
    public async Task Should_classify_every_header_in_a_controlled_vcpkg_graph_Async()
    {
        var root = CreateClassificationVcpkg();
        try
        {
            var plan = new ManifoldGenerationProvider().CreatePlan(root);
            using var document = JsonDocument.Parse(plan.ManagedSources["source-inventory.json"]);
            var actual = document.RootElement.EnumerateArray()
                .Select(static item => (
                    Header: item.GetProperty("header").GetString()!,
                    Disposition: item.GetProperty("disposition").GetString()!))
                .ToArray();
            (string Header, string Disposition)[] expected =
            [
                ("manifold/dot-segment.h", "reachable-dependency"),
                ("manifold/manifold.h", "profile-root"),
                ("manifold/mesh.h", "profile-root"),
                ("manifold/nested/relative-transitive.h", "reachable-dependency"),
                ("manifold/nested/transitive.h", "reachable-dependency"),
                ("manifold/qualified.h", "reachable-dependency"),
                ("manifold/relative.h", "reachable-dependency"),
                ("manifold/unreachable.h", "not-reachable-from-finite-profile"),
            ];

            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// Verifies invalid vcpkg package states fail before publishing output.
    /// </summary>
    /// <returns>A task that completes when validation assertions finish.</returns>
    [Test]
    public async Task Should_reject_invalid_vcpkg_header_inventories_without_output_Async()
    {
        var scenarios = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["missing-root"] = "Manifold public headers were not found",
            ["missing-status"] = "installed-package status file",
            ["missing-status-package"] = "Expected installed Manifold 3.5.2",
            ["missing-list"] = "found 0 candidate(s)",
            ["wrong-version"] = "Expected installed Manifold 3.5.2",
            ["wrong-architecture"] = "Expected installed Manifold 3.5.2",
            ["not-installed-status"] = "Expected installed Manifold 3.5.2",
            ["nonzero-port-version"] = "Expected installed Manifold 3.5.2",
            ["duplicate-status"] = "Expected installed Manifold 3.5.2",
            ["wrong-list-identity"] = "found 0 candidate(s)",
            ["ambiguous"] = "found 2 candidate(s)",
            ["empty"] = "contains no public headers",
            ["drift"] = "does not match the vcpkg package list",
        };
        foreach (var scenario in scenarios)
        {
            var root = CreateInvalidVcpkg(scenario.Key);
            var output = new DirectoryInfo(Path.Combine(root.FullName, "output"));
            Exception? failure = null;
            try
            {
                try
                {
                    await new ManifoldGenerationProvider().GenerateAsync(
                        output,
                        root,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (InvalidOperationException exception)
                {
                    failure = exception;
                }
                catch (DirectoryNotFoundException exception)
                {
                    failure = exception;
                }

                await Assert.That(failure).IsNotNull();
                await Assert.That(failure!.Message).Contains(scenario.Value);
                await Assert.That(output.Exists).IsFalse();
            }
            finally
            {
                root.Delete(true);
            }
        }
    }

    /// <summary>
    /// Verifies the retained entry points resolve only the configured environment root.
    /// </summary>
    /// <returns>A task that completes when compatibility assertions finish.</returns>
    [Test]
    [NotInParallel("VCPKG_ROOT")]
    public async Task Should_preserve_environment_backed_entry_points_Async()
    {
        var original = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        try
        {
            var vcpkgRoot = GetVcpkgRoot();
            Environment.SetEnvironmentVariable("VCPKG_ROOT", vcpkgRoot.FullName);
            var ambient = new ManifoldGenerationProvider().CreatePlan();
            var explicitPlan = new ManifoldGenerationProvider().CreatePlan(vcpkgRoot);

            await Assert.That(ambient.ManagedSources.Keys).IsEquivalentTo(explicitPlan.ManagedSources.Keys);
            await Assert.That(ambient.ManagedSources["source-inventory.json"])
                .IsEqualTo(explicitPlan.ManagedSources["source-inventory.json"]);
            await AssertPreservedArtifactHashesAsync(explicitPlan).ConfigureAwait(false);
            foreach (var source in ambient.ManagedSources.Where(static item => item.Key != "source-inventory.json"))
            {
                await Assert.That(source.Value).IsEqualTo(explicitPlan.ManagedSources[source.Key]);
            }

            foreach (var source in ambient.NativeSources)
            {
                await Assert.That(source.Value).IsEqualTo(explicitPlan.NativeSources[source.Key]);
            }

            var outputRoot = new DirectoryInfo(Path.Combine(
                Path.GetTempPath(),
                $"tedtoolkit-manifold-compatibility-{Guid.NewGuid():N}"));
            try
            {
                var ambientOutput = new DirectoryInfo(Path.Combine(outputRoot.FullName, "ambient"));
                var explicitOutput = new DirectoryInfo(Path.Combine(outputRoot.FullName, "explicit"));
                await new ManifoldGenerationProvider().GenerateAsync(ambientOutput, CancellationToken.None)
                    .ConfigureAwait(false);
                await new ManifoldGenerationProvider().GenerateAsync(
                    explicitOutput,
                    vcpkgRoot,
                    CancellationToken.None).ConfigureAwait(false);
                await AssertDirectoriesEqualAsync(ambientOutput, explicitOutput).ConfigureAwait(false);
            }
            finally
            {
                if (Directory.Exists(outputRoot.FullName))
                {
                    outputRoot.Delete(true);
                }
            }

            Environment.SetEnvironmentVariable("VCPKG_ROOT", null);
            await Assert.That(() => new ManifoldGenerationProvider().CreatePlan())
                .Throws<InvalidOperationException>();
            var missingOutput = new DirectoryInfo(Path.Combine(
                Path.GetTempPath(),
                $"tedtoolkit-manifold-missing-root-{Guid.NewGuid():N}"));
            await Assert.That(() => new ManifoldGenerationProvider().GenerateAsync(
                missingOutput,
                CancellationToken.None)).Throws<InvalidOperationException>();
            await Assert.That(missingOutput.Exists).IsFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("VCPKG_ROOT", original);
        }
    }

    /// <summary>
    /// Verifies the profile snapshots the exact operation and status enumeration values.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_lock_native_enum_values_Async()
    {
        var profile = new ManifoldProfile();
        var expectedOperations = new Dictionary<string, int>()
        {
            ["Add"] = 0,
            ["Subtract"] = 1,
            ["Intersect"] = 2,
        };
        var expectedErrors = new Dictionary<string, int>()
        {
            ["NoError"] = 0,
            ["NonFiniteVertex"] = 1,
            ["NotManifold"] = 2,
            ["VertexOutOfBounds"] = 3,
            ["PropertiesWrongLength"] = 4,
            ["MissingPositionProperties"] = 5,
            ["MergeVectorsDifferentLengths"] = 6,
            ["MergeIndexOutOfBounds"] = 7,
            ["TransformWrongLength"] = 8,
            ["RunIndexWrongLength"] = 9,
            ["FaceIDWrongLength"] = 10,
            ["InvalidConstruction"] = 11,
            ["ResultTooLarge"] = 12,
            ["InvalidTangents"] = 13,
            ["Cancelled"] = 14,
        };

        await Assert.That(profile.Operations.Count).IsEqualTo(3);
        await Assert.That(profile.NativeOperationUnderlyingType).IsEqualTo("char");
        foreach (var expected in expectedOperations)
        {
            await Assert.That(profile.Operations.ContainsKey(expected.Key)).IsTrue();
            await Assert.That(profile.Operations[expected.Key]).IsEqualTo(expected.Value);
        }

        await Assert.That(profile.Errors.Count).IsEqualTo(15);
        await Assert.That(profile.NativeErrorUnderlyingType).IsEqualTo("int");
        foreach (var expected in expectedErrors)
        {
            await Assert.That(profile.Errors.ContainsKey(expected.Key)).IsTrue();
            await Assert.That(profile.Errors[expected.Key]).IsEqualTo(expected.Value);
        }
    }

    private static DirectoryInfo GetVcpkgRoot()
    {
        return new(Environment.GetEnvironmentVariable("VCPKG_ROOT") is { Length: > 0, } root ? root : @"C:\vcpkg");
    }

    private static string[] ReadPackageHeaders(DirectoryInfo vcpkgRoot)
    {
        return File.ReadLines(Path.Combine(
                vcpkgRoot.FullName,
                "installed",
                "vcpkg",
                "info",
                "manifold_3.5.2_x64-windows.list"))
            .Select(static line => line.Replace('\\', '/'))
            .Where(static line => line.StartsWith("x64-windows/include/manifold/", StringComparison.Ordinal)
                && !line.EndsWith('/'))
            .Select(static line => line["x64-windows/include/".Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task AssertPreservedArtifactHashesAsync(ManifoldGenerationPlan plan)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["csharp/admitted-inventory.json"] = "a1f47b87310993aa7d8e592298f43c75b693f78ce9fc145841fdf5aae0312516",
            ["csharp/candidate-inventory.json"] = "28c3acc4f2df029d17d8e262e186bc96f2456ec5f9d4add8fce7b740421591ce",
            ["csharp/layout-inventory.json"] = "10bed8229f54d0adca744c8212fd2cea28b36dc7e137d63c567517dd09abe211",
            ["csharp/managed-inventory.json"] = "ed253a807f5cef558c53ffda92e9d8e5dc22a12175f31cc4156a7040b5a6628c",
            ["csharp/Manifold.Bindings.g.cs"] = "c452e996e22f3bfdc4b2f1760e7b6bb5c88d4611669c100096d7bd2d828649d0",
            ["csharp/ownership-inventory.json"] = "0d64b61091e096e1ca14a424196adb4eee91b0cf1c293ca78ccb6a12e4477e0b",
            ["csharp/profile-manifest.json"] = "eaca47e037238a10a0c3cce3c19a7ba96a7f4b8052eff914153d3e484f58dfc9",
            ["csharp/source-declaration-inventory.json"] = "28c3acc4f2df029d17d8e262e186bc96f2456ec5f9d4add8fce7b740421591ce",
            ["csharp/toolchain-inventory.json"] = "fe8ef398cd6ce22b122f0fc865dc0a4dbb8906de2bd2db2a1806e5af34b7f85a",
            ["csharp/unsupported-inventory.json"] = "cad79a0fe5c781a9a1be29283613cce96636972f963ded0d254af06a264fa099",
            ["cpp/CMakeLists.txt"] = "8e714b1521ac5fdcde97158d2b596d249d14ce317b8b7adeec12ab4827d4b27f",
            ["cpp/ManifoldProfileAdapter.cpp"] = "5eba66055fd4eea71c4289f6c034d8623a0da4ca919bf87aa9cfd34b7d1b65bd",
            ["cpp/native-inventory.json"] = "164b217105f34e884c48ef77a4af5d2620d36d2287e1bcc50c9ce551a1c522ba",
        };
        var actual = plan.ManagedSources
            .Where(static item => item.Key != "source-inventory.json")
            .ToDictionary(static item => "csharp/" + item.Key, static item => Hash(item.Value), StringComparer.Ordinal);
        foreach (var source in plan.NativeSources)
        {
            actual.Add("cpp/" + source.Key, Hash(source.Value));
        }

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        foreach (var artifact in expected)
        {
            await Assert.That(actual[artifact.Key]).IsEqualTo(artifact.Value.ToUpperInvariant());
        }
    }

    private static async Task AssertDirectoriesEqualAsync(DirectoryInfo first, DirectoryInfo second)
    {
        var firstFiles = first.EnumerateFiles("*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(first.FullName, file.FullName),
                static file => File.ReadAllBytes(file.FullName),
                StringComparer.Ordinal);
        var secondFiles = second.EnumerateFiles("*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(second.FullName, file.FullName),
                static file => File.ReadAllBytes(file.FullName),
                StringComparer.Ordinal);

        await Assert.That(firstFiles.Keys).IsEquivalentTo(secondFiles.Keys);
        foreach (var file in firstFiles)
        {
            await Assert.That(file.Value.SequenceEqual(secondFiles[file.Key])).IsTrue();
        }
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static DirectoryInfo CreateClassificationVcpkg()
    {
        var root = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            $"tedtoolkit-manifold-classification-{Guid.NewGuid():N}"));
        var includeRoot = Path.Combine(root.FullName, "installed", "x64-windows", "include");
        var manifoldRoot = Path.Combine(includeRoot, "manifold");
        var infoRoot = Path.Combine(root.FullName, "installed", "vcpkg", "info");
        Directory.CreateDirectory(Path.Combine(manifoldRoot, "nested"));
        Directory.CreateDirectory(infoRoot);
        File.WriteAllText(
            Path.Combine(manifoldRoot, "manifold.h"),
            "#include <manifold/qualified.h>\n#include \"./relative.h\"\n#include \"../outside.h\"\n");
        File.WriteAllText(Path.Combine(manifoldRoot, "mesh.h"), "");
        File.WriteAllText(Path.Combine(manifoldRoot, "qualified.h"), "#include <nested/transitive.h>\n");
        File.WriteAllText(Path.Combine(manifoldRoot, "relative.h"), "#include \"./dot/../dot-segment.h\"\n");
        File.WriteAllText(Path.Combine(manifoldRoot, "dot-segment.h"), "");
        File.WriteAllText(
            Path.Combine(manifoldRoot, "nested", "transitive.h"),
            "#include \"relative-transitive.h\"\n");
        File.WriteAllText(Path.Combine(manifoldRoot, "nested", "relative-transitive.h"), "");
        File.WriteAllText(Path.Combine(manifoldRoot, "unreachable.h"), "");
        File.WriteAllText(Path.Combine(includeRoot, "outside.h"), "");
        File.WriteAllText(
            Path.Combine(root.FullName, "installed", "vcpkg", "status"),
            "Package: manifold\nVersion: 3.5.2\nArchitecture: x64-windows\nStatus: install ok installed\n\n");
        var headers = Directory.EnumerateFiles(manifoldRoot, "*", SearchOption.AllDirectories)
            .Select(path => "x64-windows/include/" + Path.GetRelativePath(includeRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
        File.WriteAllLines(Path.Combine(infoRoot, "manifold_3.5.2_x64-windows.list"), headers);
        return root;
    }

    private static DirectoryInfo CreateInvalidVcpkg(string scenario)
    {
        var root = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            $"tedtoolkit-manifold-vcpkg-{Guid.NewGuid():N}"));
        var includeRoot = Path.Combine(root.FullName, "installed", "x64-windows", "include", "manifold");
        var infoRoot = Path.Combine(root.FullName, "installed", "vcpkg", "info");
        if (scenario != "missing-root")
        {
            Directory.CreateDirectory(includeRoot);
            File.WriteAllText(Path.Combine(includeRoot, "manifold.h"), "#include \"manifold/mesh.h\"\n");
            File.WriteAllText(Path.Combine(includeRoot, "mesh.h"), "");
        }

        Directory.CreateDirectory(infoRoot);
        var statusVersion = scenario == "wrong-version" ? "3.5.1" : "3.5.2";
        var architecture = scenario == "wrong-architecture" ? "arm64-windows" : "x64-windows";
        var status = scenario == "not-installed-status" ? "purge ok not-installed" : "install ok installed";
        var portVersion = scenario == "nonzero-port-version" ? "Port-Version: 1\n" : "";
        var statusPath = Path.Combine(root.FullName, "installed", "vcpkg", "status");
        if (scenario != "missing-status")
        {
            var package = scenario == "missing-status-package" ? "other" : "manifold";
            var statusBlock =
                $"Package: {package}\nVersion: {statusVersion}\n{portVersion}Architecture: {architecture}\nStatus: {status}\n\n";
            File.WriteAllText(
                statusPath,
                scenario == "duplicate-status" ? statusBlock + statusBlock : statusBlock);
        }

        if (scenario != "missing-list")
        {
            var headers = scenario == "empty"
                ? "x64-windows/include/manifold/\n"
                : "x64-windows/include/manifold/manifold.h\nx64-windows/include/manifold/mesh.h\n";
            if (scenario == "drift")
            {
                headers += "x64-windows/include/manifold/missing.h\n";
            }

            var listName = scenario == "wrong-list-identity"
                ? "manifold_3.5.2_arm64-windows.list"
                : "manifold_3.5.2_x64-windows.list";
            File.WriteAllText(Path.Combine(infoRoot, listName), headers);
            if (scenario == "ambiguous")
            {
                File.WriteAllText(
                    Path.Combine(infoRoot, "manifold_3.5.1_x64-windows.list"),
                    headers);
            }
        }

        return root;
    }
}
