// -----------------------------------------------------------------------
// <copyright file="FclGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using TedToolkit.CppBindings.Fcl.Generator;

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
        var first = new FclGenerationProvider().CreatePlan(vcpkgRoot);
        var second = new FclGenerationProvider().CreatePlan(vcpkgRoot);

        await Assert.That(first.ProfileId).IsEqualTo("fcl-0.7.0-obbrss-double-windows-v1");
        await Assert.That(first.NativeFunctions.Count).IsEqualTo(7);
        await Assert.That(first.NativeFunctions).IsEquivalentTo(second.NativeFunctions);
        await Assert.That(first.ManagedSources.Keys).IsEquivalentTo(second.ManagedSources.Keys);
        await Assert.That(first.NativeSources.Keys).IsEquivalentTo(second.NativeSources.Keys);
        foreach (var source in first.ManagedSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.ManagedSources[source.Key]);
        }

        foreach (var source in first.NativeSources)
        {
            await Assert.That(source.Value).IsEqualTo(second.NativeSources[source.Key]);
        }

        var managed = first.ManagedSources["Fcl.Bindings.g.cs"];
        var native = first.NativeSources["FclProfileAdapter.cpp"];
        await Assert.That(managed).Contains("ReadOnlySpan<nuint> triangleIndices");
        await Assert.That(managed).Contains("public readonly struct FclVector3");
        await Assert.That(managed).Contains("GC.SuppressFinalize(owner);");
        await Assert.That(native).Contains("static_assert(sizeof(FclVector3Transport) == 24");
        await Assert.That(native).Contains("request.ccd_motion_type = fcl::CCDM_LINEAR");
        await Assert.That(native).Contains("request.ccd_solver_type = fcl::CCDC_CONSERVATIVE_ADVANCEMENT");
        await Assert.That(native).Contains("fallbackRequest.ccd_motion_type = fcl::CCDM_TRANS");
        await Assert.That(native).Contains("fallbackRequest.ccd_solver_type = fcl::CCDC_POLYNOMIAL_SOLVER");
    }

    /// <summary>Verifies vcpkg is the complete public-header inventory authority.</summary>
    /// <returns>A task that completes when inventory assertions finish.</returns>
    [Test]
    public async Task Should_resolve_complete_header_inventory_from_vcpkg_Async()
    {
        var vcpkgRoot = GetVcpkgRoot();
        var plan = new FclGenerationProvider().CreatePlan(vcpkgRoot);
        using var document = JsonDocument.Parse(plan.ManagedSources["source-inventory.json"]);
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
            var plan = new FclGenerationProvider().CreatePlan(root);
            using var document = JsonDocument.Parse(plan.ManagedSources["source-inventory.json"]);
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
                    await new FclGenerationProvider().GenerateAsync(
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
            var ambient = new FclGenerationProvider().CreatePlan();
            var explicitPlan = new FclGenerationProvider().CreatePlan(vcpkgRoot);

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
                $"tedtoolkit-fcl-compatibility-{Guid.NewGuid():N}"));
            try
            {
                var ambientOutput = new DirectoryInfo(Path.Combine(outputRoot.FullName, "ambient"));
                var explicitOutput = new DirectoryInfo(Path.Combine(outputRoot.FullName, "explicit"));
                await new FclGenerationProvider().GenerateAsync(ambientOutput, CancellationToken.None)
                    .ConfigureAwait(false);
                await new FclGenerationProvider().GenerateAsync(explicitOutput, vcpkgRoot, CancellationToken.None)
                    .ConfigureAwait(false);
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
            await Assert.That(() => new FclGenerationProvider().CreatePlan())
                .Throws<InvalidOperationException>();
            var missingOutput = new DirectoryInfo(Path.Combine(
                Path.GetTempPath(),
                $"tedtoolkit-fcl-missing-root-{Guid.NewGuid():N}"));
            await Assert.That(() => new FclGenerationProvider().GenerateAsync(missingOutput, CancellationToken.None))
                .Throws<InvalidOperationException>();
            await Assert.That(missingOutput.Exists).IsFalse();
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

    private static async Task AssertPreservedArtifactHashesAsync(FclGenerationPlan plan)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["csharp/admitted-inventory.json"] = "012371a71f17fea2e4cf29eda24c1a2e0b1aab5bda6ca3533fd7d25e8b65f19b",
            ["csharp/candidate-inventory.json"] = "92f9c5332583877641c79938ab91c7634d8a8ba26e2c5082a8b80e51deaf45a3",
            ["csharp/Fcl.Bindings.g.cs"] = "aea95a13ff9150c8a42528c995e255f51d01de8dbf5852e8867b3f43322e89ec",
            ["csharp/layout-inventory.json"] = "6e7e7f700b22c01434a27e526242fa4831fdf88b3d53539b31e0c2ae237ad704",
            ["csharp/managed-inventory.json"] = "012371a71f17fea2e4cf29eda24c1a2e0b1aab5bda6ca3533fd7d25e8b65f19b",
            ["csharp/ownership-inventory.json"] = "4bfa174f8f83ad5ff9cf18e396e29ca00b73ad147d666f3ee004e6827d4f6b5b",
            ["csharp/profile-manifest.json"] = "ee7a1f99a227df39dc1d1db3f3b15d274b59237255845b111762a8a074fda729",
            ["csharp/source-declaration-inventory.json"] = "dd132b5847af036b14054570975f719241e3f6c5a675735b8d551f0cde9cca70",
            ["csharp/toolchain-inventory.json"] = "58dbeac44d20fa3ed76650071088edfa63984464a6e871581e211070114eaeb9",
            ["csharp/unsupported-inventory.json"] = "f73435a64d0d9e72d8cabfb834b18bf0d6e90c6df31216c8a4099b34c958b086",
            ["cpp/CMakeLists.txt"] = "0ab605a5cf67153e603981361ca1b985f3681c2d8064b000c2551902a1124bf3",
            ["cpp/FclProfileAdapter.cpp"] = "40b92424ccabf48da18a6669ccf65a4af761db977a836b5a50e7d6607087edea",
            ["cpp/native-inventory.json"] = "57bbc0fc677e7a049337c6d6382862f8a76f690a059765c61adf6072a108bc09",
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
