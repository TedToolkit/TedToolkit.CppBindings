// -----------------------------------------------------------------------
// <copyright file="FclGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Fcl.Generator;
using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Fcl.Generator.Tests;

/// <summary>Verifies the finite FCL generation profile.</summary>
internal sealed class FclGenerationProviderTests
{
    /// <summary>Verifies deterministic complete managed/native plans.</summary>
    /// <returns>A task that completes when plan assertions finish.</returns>
    [Test]
    public async Task Should_create_byte_identical_complete_plans_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var firstProvider = new FclGenerationProvider(vcpkgRoot);
        var secondProvider = new FclGenerationProvider(vcpkgRoot);
        var first = await firstProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var second = await secondProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var firstManaged = await RenderAsync(first.CSharpSources).ConfigureAwait(false);
        var secondManaged = await RenderAsync(second.CSharpSources).ConfigureAwait(false);
        var firstNative = await RenderAsync(first.CppSources).ConfigureAwait(false);
        var secondNative = await RenderAsync(second.CppSources).ConfigureAwait(false);

        await Assert.That(firstProvider.Profile.ProfileId).IsEqualTo("fcl-0.7.0-obbrss-double-windows-v2");
        await Assert.That(firstProvider).IsAssignableTo<SemanticGenerationProvider>();
        await Assert.That(first.NativeExports.Count).IsEqualTo(7);
        string[] expectedExports =
        [
            "Fcl_NativeError_Clear",
            "Fcl_Model_Destroy",
            "Fcl_Model_Create",
            "Fcl_ContinuousCollision_Query",
            "Fcl_Lifetime_Reset",
            "Fcl_Lifetime_CreateCount",
            "Fcl_Lifetime_DestroyCount",
        ];
        await Assert.That(first.NativeExports.SequenceEqual(expectedExports, StringComparer.Ordinal)).IsTrue();
        await Assert.That(first.NativeExports.SequenceEqual(second.NativeExports, StringComparer.Ordinal)).IsTrue();
        await Assert.That(firstManaged.Keys).IsEquivalentTo(secondManaged.Keys);
        await Assert.That(firstNative.Keys).IsEquivalentTo(secondNative.Keys);
        foreach (var source in firstManaged)
        {
            await Assert.That(source.Value).IsEqualTo(secondManaged[source.Key]);
        }

        foreach (var source in firstNative)
        {
            await Assert.That(source.Value).IsEqualTo(secondNative[source.Key]);
        }

        var managed = firstManaged["Fcl.Bindings.g.cs"];
        var native = firstNative["FclProfileAdapter.cpp"];
        await Assert.That(managed).Contains("ReadOnlySpan<nuint> triangleIndices");
        await Assert.That(managed).Contains("public readonly struct FclVector3");
        await Assert.That(managed).Contains("GC.SuppressFinalize(owner);");
        await Assert.That(native).Contains("static_assert(sizeof(FclVector3Transport) == 24");
        await Assert.That(native).Contains("request.ccd_motion_type = fcl::CCDM_LINEAR");
        await Assert.That(native).Contains("request.ccd_solver_type = fcl::CCDC_CONSERVATIVE_ADVANCEMENT");
        await Assert.That(native).Contains("fallbackRequest.ccd_motion_type = fcl::CCDM_TRANS");
        await Assert.That(native).Contains("fallbackRequest.ccd_solver_type = fcl::CCDC_POLYNOMIAL_SOLVER");
        await Assert.That(native).Contains("SetError(error, 3, \"std::underflow_error\"");
        await Assert.That(native).Contains("SetError(error, 8, \"std::exception\"");
        await Assert.That(native).DoesNotContain("SetError(error, 9, \"std::exception\"");
        await Assert.That(managed).DoesNotContain("class NativeApi");
        await Assert.That(native).DoesNotContain("NativeApi_GetFunctionTable");
        await Assert.That(typeof(FclGenerationProvider).GetMethod("GenerateAsync")).IsNull();
        await Assert.That(typeof(FclGenerationProvider).Assembly.GetType(
            "TedToolkit.CppBindings.Fcl.Generator.FclGenerationPlan")).IsNull();
        await Assert.That(typeof(FclGenerationProvider).Assembly.GetType(
            "TedToolkit.CppBindings.Fcl.Generator.FclSourceRenderer")).IsNull();
    }

    /// <summary>Verifies vcpkg is the complete public-header inventory authority.</summary>
    /// <returns>A task that completes when inventory assertions finish.</returns>
    [Test]
    public async Task Should_resolve_complete_header_inventory_from_vcpkg_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var plan = await new FclGenerationProvider(vcpkgRoot).CreatePlanAsync(CancellationToken.None)
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
        await Assert.That(actual.Single(static item => item.Header == "fcl/fcl.h").Disposition)
            .IsEqualTo("profile-root");
        await Assert.That(actual.Single(static item => item.Header == "fcl/geometry/bvh/BVH_model.h").Disposition)
            .IsEqualTo("reachable-dependency");
        await Assert.That(actual.Single(static item => item.Header.EndsWith(
            "convexity_based_algorithm/support.h",
            StringComparison.Ordinal)).Disposition).IsEqualTo("reachable-dependency");
        await Assert.That(typeof(FclGenerationProvider).Assembly.GetManifestResourceNames()
            .Any(static name => name.EndsWith("fcl-0.7.0-obbrss-double-windows-v1.json", StringComparison.Ordinal)))
            .IsFalse();
    }

    /// <summary>Verifies every installed header is classified by the complete include-closure rules.</summary>
    /// <returns>A task that completes when classification assertions finish.</returns>
    [Test]
    public async Task Should_classify_every_header_in_a_controlled_vcpkg_graph_Async()
    {
        var root = CreateClassificationVcpkg();
        try
        {
            var plan = await new FclGenerationProvider(root).CreatePlanAsync(CancellationToken.None)
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
                ("fcl/dot-segment.h", "reachable-dependency"),
                ("fcl/fcl.h", "profile-root"),
                ("fcl/nested/relative-transitive.h", "reachable-dependency"),
                ("fcl/nested/transitive.h", "reachable-dependency"),
                ("fcl/qualified.h", "reachable-dependency"),
                ("fcl/relative.h", "reachable-dependency"),
                ("fcl/unreachable.h", "not-reachable-from-finite-profile"),
            ];

            await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>Verifies invalid vcpkg package states fail before publishing output.</summary>
    /// <returns>A task that completes when validation assertions finish.</returns>
    [Test]
    public async Task Should_reject_invalid_vcpkg_header_inventories_without_output_Async()
    {
        var scenarios = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["missing-root"] = "FCL public headers were not found",
            ["missing-status"] = "installed-package status file",
            ["missing-status-package"] = "Expected installed FCL 0.7.0#5",
            ["missing-list"] = "found 0 candidate(s)",
            ["wrong-version"] = "Expected installed FCL 0.7.0#5",
            ["wrong-port-version"] = "Expected installed FCL 0.7.0#5",
            ["wrong-architecture"] = "Expected installed FCL 0.7.0#5",
            ["not-installed-status"] = "Expected installed FCL 0.7.0#5",
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
                    _ = await new FclGenerationProvider(root).CreatePlanAsync(CancellationToken.None)
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

    /// <summary>Verifies the retained entry points resolve only the configured environment root.</summary>
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
            var ambient = await new FclGenerationProvider().CreatePlanAsync(CancellationToken.None)
                .ConfigureAwait(false);
            var explicitPlan = await new FclGenerationProvider(vcpkgRoot).CreatePlanAsync(CancellationToken.None)
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
            await Assert.That(() => new FclGenerationProvider())
                .Throws<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("VCPKG_ROOT", original);
        }
    }

    /// <summary>Verifies every locked BVH status name and value.</summary>
    /// <returns>A task that completes when status assertions finish.</returns>
    [Test]
    public async Task Should_lock_every_bvh_return_code_Async()
    {
        var expected = new Dictionary<string, int>()
        {
            ["BVH_OK"] = 0,
            ["BVH_ERR_MODEL_OUT_OF_MEMORY"] = -1,
            ["BVH_ERR_BUILD_OUT_OF_SEQUENCE"] = -2,
            ["BVH_ERR_BUILD_EMPTY_MODEL"] = -3,
            ["BVH_ERR_BUILD_EMPTY_PREVIOUS_FRAME"] = -4,
            ["BVH_ERR_UNSUPPORTED_FUNCTION"] = -5,
            ["BVH_ERR_UNUPDATED_MODEL"] = -6,
            ["BVH_ERR_INCORRECT_DATA"] = -7,
            ["BVH_ERR_UNKNOWN"] = -8,
        };
        var actual = new FclProfile().BvhReturnCodes;

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        foreach (var item in expected)
        {
            await Assert.That(actual.ContainsKey(item.Key)).IsTrue();
            await Assert.That(actual[item.Key]).IsEqualTo(item.Value);
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
                "fcl_0.7.0_x64-windows.list"))
            .Select(static line => line.Replace('\\', '/'))
            .Where(static line => line.StartsWith("x64-windows/include/fcl/", StringComparison.Ordinal)
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
            $"tedtoolkit-fcl-classification-{Guid.NewGuid():N}"));
        var includeRoot = Path.Combine(root.FullName, "installed", "x64-windows", "include");
        var fclRoot = Path.Combine(includeRoot, "fcl");
        var infoRoot = Path.Combine(root.FullName, "installed", "vcpkg", "info");
        Directory.CreateDirectory(Path.Combine(fclRoot, "nested"));
        Directory.CreateDirectory(infoRoot);
        File.WriteAllText(
            Path.Combine(fclRoot, "fcl.h"),
            "#include <fcl/qualified.h>\n#include \"./relative.h\"\n#include \"../outside.h\"\n");
        File.WriteAllText(Path.Combine(fclRoot, "qualified.h"), "#include <nested/transitive.h>\n");
        File.WriteAllText(Path.Combine(fclRoot, "relative.h"), "#include \"./dot/../dot-segment.h\"\n");
        File.WriteAllText(Path.Combine(fclRoot, "dot-segment.h"), "");
        File.WriteAllText(
            Path.Combine(fclRoot, "nested", "transitive.h"),
            "#include \"relative-transitive.h\"\n");
        File.WriteAllText(Path.Combine(fclRoot, "nested", "relative-transitive.h"), "");
        File.WriteAllText(Path.Combine(fclRoot, "unreachable.h"), "");
        File.WriteAllText(Path.Combine(includeRoot, "outside.h"), "");
        File.WriteAllText(
            Path.Combine(root.FullName, "installed", "vcpkg", "status"),
            "Package: fcl\nVersion: 0.7.0\nPort-Version: 5\nArchitecture: x64-windows\nStatus: install ok installed\n\n");
        var headers = Directory.EnumerateFiles(fclRoot, "*", SearchOption.AllDirectories)
            .Select(path => "x64-windows/include/" + Path.GetRelativePath(includeRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
        File.WriteAllLines(Path.Combine(infoRoot, "fcl_0.7.0_x64-windows.list"), headers);
        return root;
    }

    private static DirectoryInfo CreateInvalidVcpkg(string scenario)
    {
        var root = new DirectoryInfo(Path.Combine(
            Path.GetTempPath(),
            $"tedtoolkit-fcl-vcpkg-{Guid.NewGuid():N}"));
        var includeRoot = Path.Combine(root.FullName, "installed", "x64-windows", "include", "fcl");
        var infoRoot = Path.Combine(root.FullName, "installed", "vcpkg", "info");
        if (scenario != "missing-root")
        {
            Directory.CreateDirectory(includeRoot);
            File.WriteAllText(Path.Combine(includeRoot, "fcl.h"), "#include \"fcl/detail.h\"\n");
            File.WriteAllText(Path.Combine(includeRoot, "detail.h"), "");
        }

        Directory.CreateDirectory(infoRoot);
        var statusVersion = scenario == "wrong-version" ? "0.6.1" : "0.7.0";
        var portVersion = scenario == "wrong-port-version" ? "4" : "5";
        var architecture = scenario == "wrong-architecture" ? "arm64-windows" : "x64-windows";
        var status = scenario == "not-installed-status" ? "purge ok not-installed" : "install ok installed";
        var statusPath = Path.Combine(root.FullName, "installed", "vcpkg", "status");
        if (scenario != "missing-status")
        {
            var package = scenario == "missing-status-package" ? "other" : "fcl";
            File.WriteAllText(
                statusPath,
                $"Package: {package}\nVersion: {statusVersion}\nPort-Version: {portVersion}\nArchitecture: {architecture}\nStatus: {status}\n\n");
        }

        if (scenario != "missing-list")
        {
            var headers = scenario == "empty"
                ? "x64-windows/include/fcl/\n"
                : "x64-windows/include/fcl/fcl.h\nx64-windows/include/fcl/detail.h\n";
            if (scenario == "drift")
            {
                headers += "x64-windows/include/fcl/missing.h\n";
            }

            var listName = scenario == "wrong-list-identity"
                ? "fcl_0.7.0_arm64-windows.list"
                : "fcl_0.7.0_x64-windows.list";
            File.WriteAllText(Path.Combine(infoRoot, listName), headers);
            if (scenario == "ambiguous")
            {
                File.WriteAllText(Path.Combine(infoRoot, "fcl_0.6.1_x64-windows.list"), headers);
            }
        }

        return root;
    }
}