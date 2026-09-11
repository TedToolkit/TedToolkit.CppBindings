// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Manifold.Generator;

namespace TedToolkit.CppBindings.Manifold.Generator.Tests;

/// <summary>
/// Verifies the locked finite Manifold profile and shared semantic generation contract.
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
        var firstProvider = new ManifoldGenerationProvider(vcpkgRoot);
        var secondProvider = new ManifoldGenerationProvider(vcpkgRoot);
        var first = await firstProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var second = await secondProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var firstManaged = await RenderAsync(first.CSharpSources).ConfigureAwait(false);
        var secondManaged = await RenderAsync(second.CSharpSources).ConfigureAwait(false);
        var firstNative = await RenderAsync(first.CppSources).ConfigureAwait(false);
        var secondNative = await RenderAsync(second.CppSources).ConfigureAwait(false);

        await Assert.That(firstProvider.Profile.ProfileId).IsEqualTo("manifold-3.5.2-windows-v2");
        await Assert.That(firstProvider).IsAssignableTo<SemanticGenerationProvider>();
        await Assert.That(first.NativeExports.Count).IsEqualTo(9);
        string[] expectedExports =
        [
            "Manifold_NativeError_Clear",
            "Manifold_Destroy",
            "Manifold_Create",
            "Manifold_Status",
            "Manifold_Boolean",
            "Manifold_Translate",
            "Manifold_NumTri",
            "Manifold_GetMeshCounts",
            "Manifold_CopyMesh",
        ];
        await Assert.That(first.NativeExports.SequenceEqual(expectedExports, StringComparer.Ordinal)).IsTrue();
        await Assert.That(first.NativeExports.SequenceEqual(second.NativeExports, StringComparer.Ordinal)).IsTrue();
        await Assert.That(firstManaged.Keys).IsEquivalentTo(secondManaged.Keys);
        await Assert.That(firstNative.Keys).IsEquivalentTo(secondNative.Keys);
        await Assert.That(firstManaged.Keys).Contains("layout-inventory.json");
        await Assert.That(firstManaged.Keys).Contains("ownership-inventory.json");
        await Assert.That(firstManaged.Keys).Contains("source-declaration-inventory.json");
        await Assert.That(firstManaged.Keys).Contains("toolchain-inventory.json");
        foreach (var source in firstManaged)
        {
            await Assert.That(source.Value).IsEqualTo(secondManaged[source.Key]);
        }

        foreach (var source in firstNative)
        {
            await Assert.That(source.Value).IsEqualTo(secondNative[source.Key]);
        }

        var managed = firstManaged["Manifold.Bindings.g.cs"];
        var operation = firstManaged["ManifoldOp.g.cs"];
        var native = firstNative["ManifoldProfileAdapter.cpp"];
        await Assert.That(operation).Contains("public enum ManifoldOp : sbyte");
        await Assert.That(operation).Contains("Add = 0");
        await Assert.That(operation).Contains("Subtract = 1");
        await Assert.That(operation).Contains("Intersect = 2");
        await Assert.That(managed).Contains("public static global::TedToolkit.CppBindings.Owned<Manifold> Create");
        await Assert.That(managed).Contains("ReadOnlySpan<double> vertexCoordinates");
        await Assert.That(managed).Contains("ReadOnlySpan<ulong> triangleIndices");
        await Assert.That(managed).Contains("GC.SuppressFinalize(owner);");
        await Assert.That(managed).Contains("ManifoldError Status(");
        await Assert.That(managed).Contains("Owned<Manifold> Boolean(");
        await Assert.That(managed).Contains("Owned<Manifold> Translate(");
        await Assert.That(managed).Contains("nuint NumTri(");
        await Assert.That(managed).Contains("ManifoldMeshData GetMesh(");
        await Assert.That(managed).Contains("vertexCoordinateCount > int.MaxValue");
        await Assert.That(native).Contains("static_assert(sizeof(ManifoldAdapter) == 8");
        await Assert.That(native).Contains("Manifold::Error::Cancelled) == 14");
        await Assert.That(native).Contains("extern \"C\" std::size_t Manifold_NumTri(");
        foreach (var export in expectedExports)
        {
            await Assert.That(native).Contains(export + "(");
        }

        await Assert.That(native).Contains("SetError(error, 3, \"std::underflow_error\"");
        await Assert.That(native).Contains("SetError(error, 8, \"std::exception\"");
        await Assert.That(native).DoesNotContain("SetError(error, 9, \"std::exception\"");
        await Assert.That(managed).DoesNotContain("class NativeApi");
        await Assert.That(native).DoesNotContain("NativeApi_GetFunctionTable");
        await Assert.That(typeof(ManifoldGenerationProvider).GetMethod("GenerateAsync")).IsNull();
        await Assert.That(typeof(ManifoldGenerationProvider).Assembly.GetType(
            "TedToolkit.CppBindings.Manifold.Generator.ManifoldGenerationPlan")).IsNull();
        await Assert.That(typeof(ManifoldGenerationProvider).Assembly.GetType(
            "TedToolkit.CppBindings.Manifold.Generator.ManifoldSourceRenderer")).IsNull();
    }

    /// <summary>
    /// Verifies that vcpkg is the complete public-header inventory authority.
    /// </summary>
    /// <returns>A task that completes when inventory assertions finish.</returns>
    [Test]
    public async Task Should_resolve_complete_header_inventory_from_vcpkg_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var plan = await new ManifoldGenerationProvider(vcpkgRoot).CreatePlanAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var managed = await RenderAsync(plan.CSharpSources).ConfigureAwait(false);
        using var document = JsonDocument.Parse(managed["source-inventory.json"]);
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
            var plan = await new ManifoldGenerationProvider(root).CreatePlanAsync(CancellationToken.None)
                .ConfigureAwait(false);
            var managed = await RenderAsync(plan.CSharpSources).ConfigureAwait(false);
            using var document = JsonDocument.Parse(managed["source-inventory.json"]);
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
                    _ = await new ManifoldGenerationProvider(root).CreatePlanAsync(CancellationToken.None)
                        .ConfigureAwait(false);
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
            var ambient = await new ManifoldGenerationProvider().CreatePlanAsync(CancellationToken.None)
                .ConfigureAwait(false);
            var explicitPlan = await new ManifoldGenerationProvider(vcpkgRoot).CreatePlanAsync(CancellationToken.None)
                .ConfigureAwait(false);
            var ambientManaged = await RenderAsync(ambient.CSharpSources).ConfigureAwait(false);
            var explicitManaged = await RenderAsync(explicitPlan.CSharpSources).ConfigureAwait(false);
            var ambientNative = await RenderAsync(ambient.CppSources).ConfigureAwait(false);
            var explicitNative = await RenderAsync(explicitPlan.CppSources).ConfigureAwait(false);

            await Assert.That(ambientManaged.Keys).IsEquivalentTo(explicitManaged.Keys);
            await Assert.That(ambientManaged["source-inventory.json"])
                .IsEqualTo(explicitManaged["source-inventory.json"]);
            foreach (var source in ambientManaged.Where(static item => item.Key != "source-inventory.json"))
            {
                await Assert.That(source.Value).IsEqualTo(explicitManaged[source.Key]);
            }

            foreach (var source in ambientNative)
            {
                await Assert.That(source.Value).IsEqualTo(explicitNative[source.Key]);
            }

            Environment.SetEnvironmentVariable("VCPKG_ROOT", null);
            await Assert.That(() => new ManifoldGenerationProvider())
                .Throws<InvalidOperationException>();
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

    private static async Task<IReadOnlyDictionary<string, string>> RenderAsync(
        IReadOnlyList<GeneratedSource> sources)
    {
        var rendered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var writer = new StringWriter();
            await using (writer.ConfigureAwait(false))
            {
                await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
                rendered.Add(source.RelativePath, writer.ToString());
            }
        }

        return rendered;
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