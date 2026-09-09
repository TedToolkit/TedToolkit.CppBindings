// -----------------------------------------------------------------------
// <copyright file="CgalGenerationProviderTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using TedToolkit.CppBindings.Cgal.Generator;
using TedToolkit.CppBindings.Generator;

namespace TedToolkit.CppBindings.Cgal.Generator.Tests;

/// <summary>
/// Verifies the locked finite CGAL profile against independently observed installed headers.
/// </summary>
internal sealed class CgalGenerationProviderTests
{
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Verifies the Generator uses vcpkg instead of carrying a second CGAL header inventory authority.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_resolve_header_inventory_from_vcpkg_package_list_Async()
    {
        var provider = CreateProvider(requireLockedToolchain: false);

        await Assert.That(provider.Inventory.Sources.Select(static item => item.Header))
            .IsEquivalentTo(EnumerateVcpkgPackageHeaders());
        await Assert.That(typeof(CgalGenerationProvider).Assembly.GetManifestResourceNames()
            .Any(static name => name.Contains(".headers.", StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>
    /// Verifies locked inventory validation fails when vcpkg package metadata is unavailable.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_require_vcpkg_package_list_for_locked_inventory_Async()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tedtoolkit-cgal-vcpkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "installed", "x64-windows", "include", "CGAL"));
        InvalidOperationException? failure = null;
        try
        {
            try
            {
                _ = CreateProvider(requireLockedToolchain: false, vcpkgRoot: root);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message).Contains("vcpkg installed-package list directory");
    }

    /// <summary>
    /// Verifies two real-header runs produce byte-identical discovered inventories and Shared outputs.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_generate_the_locked_profile_deterministically_Async()
    {
        var firstProvider = CreateProvider();
        var secondProvider = CreateProvider();

        var firstPlan = await firstProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var secondPlan = await secondProvider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var firstManaged = await RenderAsync(firstPlan.CSharpSources).ConfigureAwait(false);
        var secondManaged = await RenderAsync(secondPlan.CSharpSources).ConfigureAwait(false);
        var firstNative = await RenderAsync(firstPlan.CppSources).ConfigureAwait(false);
        var secondNative = await RenderAsync(secondPlan.CppSources).ConfigureAwait(false);
        var independentlyEnumeratedHeaders = EnumerateInstalledHeaders();
        var vcpkgPackageHeaders = EnumerateVcpkgPackageHeaders();

        await Assert.That(firstProvider.Profile.ProfileId).IsEqualTo("epick-windows-v1");
        await Assert.That(firstProvider.Profile.CgalVersion).IsEqualTo("6.2");
        await Assert.That(firstProvider.Inventory.Sources.Select(static item => item.Header))
            .IsEquivalentTo(independentlyEnumeratedHeaders);
        await Assert.That(firstProvider.Inventory.Sources.Select(static item => item.Header))
            .IsEquivalentTo(vcpkgPackageHeaders);
        await Assert.That(firstProvider.Inventory.Sources.Count(static item => item.Disposition == "profile-root"))
            .IsEqualTo(firstProvider.Profile.SelectedHeaders.Count);
        await Assert.That(firstProvider.Inventory.Sources.Count(static item => item.Disposition == "reachable-dependency"))
            .IsGreaterThan(0);
        await Assert.That(firstProvider.Inventory.Candidates.Count)
            .IsGreaterThan(firstProvider.Profile.Declarations.Count);
        await Assert.That(firstProvider.Inventory.SourceDeclarations.Count)
            .IsGreaterThan(firstProvider.Inventory.Candidates.Count);
        await Assert.That(firstProvider.Inventory.Admitted.Count + firstProvider.Inventory.Unsupported.Count)
            .IsEqualTo(firstProvider.Inventory.Candidates.Count);
        await Assert.That(firstProvider.Inventory.Admitted.Count)
            .IsEqualTo(firstProvider.Profile.Declarations.Count);
        await Assert.That(firstProvider.Inventory.Unsupported).IsNotEmpty();
        await Assert.That(firstProvider.Inventory.Candidates.Select(static item => item.Id).Distinct().Count())
            .IsEqualTo(firstProvider.Inventory.Candidates.Count);
        await Assert.That(firstProvider.Inventory.SourceDeclarations.Any(static item =>
            item.NativeSignature.Contains("Point_2::dimension", StringComparison.Ordinal))).IsTrue();
        await Assert.That(firstProvider.Inventory.SourceDeclarations.Any(static item =>
            item.NativeSignature.Contains("Point_2::homogeneous", StringComparison.Ordinal))).IsTrue();
        await Assert.That(firstProvider.Inventory.SourceDeclarations.Any(static item =>
            item.NativeSignature.Contains("Point_2::bbox", StringComparison.Ordinal))).IsTrue();
        await Assert.That(firstProvider.Inventory.SourceDeclarations.Any(static item =>
            item.NativeSignature.Contains("Point_2::transform", StringComparison.Ordinal))).IsTrue();
        await Assert.That(firstProvider.Inventory.Unsupported.Any(static item =>
            item.NativeSignature.Contains("CGAL::do_overlap", StringComparison.Ordinal))).IsTrue();
        await Assert.That(firstProvider.Inventory.Candidates.Any(static item => item.Kind is
            "Namespace" or "FunctionTemplate" or "ClassTemplate" or "TemplateTypeParameter")).IsFalse();
        await Assert.That(firstProvider.Inventory.Toolchain.Cgal).IsEqualTo("6.2");
        await Assert.That(firstProvider.Inventory.Toolchain.CgalAbi).IsNotEmpty();
        await Assert.That(firstProvider.Inventory.Toolchain.CMake).IsEqualTo("4.4.3");
        await Assert.That(firstProvider.Inventory.Toolchain.Msvc).IsEqualTo("19.51.36256");
        await Assert.That(Hash(firstManaged)).IsEqualTo(Hash(secondManaged));
        await Assert.That(Hash(firstNative)).IsEqualTo(Hash(secondNative));
        await Assert.That(firstPlan.NativeExports.SequenceEqual(secondPlan.NativeExports, StringComparer.Ordinal)).IsTrue();
        await Assert.That(firstPlan.NativeExports.SequenceEqual(
            firstPlan.NativeExports.Order(StringComparer.Ordinal),
            StringComparer.Ordinal)).IsTrue();
    }

    /// <summary>
    /// Verifies Shared emits every admitted declaration's paired artifact and export inventory.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_emit_the_nonempty_shared_semantic_model_and_paired_artifacts_Async()
    {
        var provider = CreateProvider();
        var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var managed = await RenderAsync(plan.CSharpSources).ConfigureAwait(false);
        var native = await RenderAsync(plan.CppSources).ConfigureAwait(false);

        await Assert.That(managed.Keys).Contains("Point_2.g.cs");
        await Assert.That(managed.Keys).Contains("Point_3.g.cs");
        await Assert.That(managed.Keys).Contains("Segment_2.g.cs");
        await Assert.That(managed.Keys).Contains("Kernel_API.g.cs");
        await Assert.That(managed.Keys).Contains("Cgal.ResultProjection.g.cs");
        await Assert.That(native.Keys).Contains("TedToolkit_CppBindings_Cgal_Point_2.cpp");
        await Assert.That(native.Keys).Contains("TedToolkit_CppBindings_Cgal_Kernel_API.cpp");
        await Assert.That(native.Keys).Contains("CgalProfileAdapter.hpp");
        await Assert.That(native.Keys).Contains("CgalNativeError.cpp");
        await Assert.That(managed["Point_2.g.cs"]).Contains("NativeApi.GetFunction");
        await Assert.That(native["TedToolkit_CppBindings_Cgal_Point_2.cpp"])
            .Contains("extern \"C\" double Cgal_Point2_Cartesian");
        await Assert.That(managed["Point_2.g.cs"])
            .Contains("public static ref readonly double X(this in Point_2 self)");
        await Assert.That(managed["Segment_2.g.cs"])
            .Contains("public static ref readonly Point_2 Source(this in Segment_2 self)");
        await Assert.That(managed["Point_2.g.cs"]).DoesNotContain("public double StorageX");
        await Assert.That(managed["Segment_2.g.cs"]).DoesNotContain("public Point_2 StorageSource");
        await Assert.That(plan.NativeExports).Contains("Cgal_Point2_Create");
        await Assert.That(plan.NativeExports).Contains("Cgal_Point2_X");
        await Assert.That(plan.NativeExports).Contains("Cgal_Segment2_Source");
        await Assert.That(plan.NativeExports).Contains("Cgal_Segment2_Intersection");
        await Assert.That(plan.NativeExports.Count).IsEqualTo(17);
        await Assert.That(provider.Inventory.ManagedArtifacts
            .Select(static item => item.RelativePath).Distinct().All(managed.ContainsKey)).IsTrue();
        await Assert.That(provider.Inventory.NativeArtifacts
            .Select(static item => item.RelativePath).Distinct().All(native.ContainsKey)).IsTrue();
        await Assert.That(provider.Inventory.ManagedArtifacts.Select(static item => item.DeclarationId))
            .IsEquivalentTo(provider.Inventory.Admitted.Select(static item => item.Id));
        await Assert.That(provider.Inventory.NativeArtifacts.Select(static item => item.DeclarationId))
            .IsEquivalentTo(provider.Inventory.Admitted.Select(static item => item.Id));
        await Assert.That(provider.Inventory.NativeArtifacts.All(item =>
            native[item.RelativePath].Contains(item.Symbol, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// Verifies provider-local failures precede the fixed Shared standard catch sequence.
    /// </summary>
    /// <returns>A task that completes when native error emission assertions finish.</returns>
    [Test]
    public async Task Should_emit_shared_standard_errors_after_cgal_local_errors_without_locked_toolchain_Async()
    {
        var provider = CreateProvider(requireLockedToolchain: false);
        var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
        var native = await RenderAsync(plan.CppSources).ConfigureAwait(false);
        var source = native["TedToolkit_CppBindings_Cgal_Point_2.cpp"];
        string[] catches =
        [
            "catch (const CGAL::Error_exception& exception)",
            "catch (const CGAL::Precondition_exception& exception)",
            "catch (const CGAL::Postcondition_exception& exception)",
            "catch (const CGAL::Assertion_exception& exception)",
            "catch (const CGAL::Test_exception& exception)",
            "catch (const CGAL::Warning_exception& exception)",
            "catch (const CGAL::Failure_exception& exception)",
            "catch (const std::bad_alloc& exception)",
            "catch (const std::out_of_range& exception)",
            "catch (const std::overflow_error& exception)",
            "catch (const std::underflow_error& exception)",
            "catch (const std::invalid_argument& exception)",
            "catch (const std::domain_error& exception)",
            "catch (const std::logic_error& exception)",
            "catch (const std::exception& exception)",
            "catch (...)",
        ];
        var positions = catches.Select(item => source.IndexOf(item, StringComparison.Ordinal)).ToArray();

        await Assert.That(positions.All(static position => position >= 0)).IsTrue();
        await Assert.That(positions.SequenceEqual(positions.Order())).IsTrue();
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 10, \"CGAL::Error_exception\"");
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 16, \"CGAL::Failure_exception\"");
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 7, \"std::overflow_error\"");
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 3, \"std::underflow_error\"");
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 8, \"std::exception\"");
        await Assert.That(source).Contains("Cgal_NativeError_Set(__error, 255, nullptr, nullptr");
        await Assert.That(source).DoesNotContain("Cgal_NativeError_Set(__error, 9, \"std::exception\"");
    }

    /// <summary>
    /// Verifies an explicit profile document is accepted without mutating the embedded default authority.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_generate_an_explicit_finite_profile_and_snapshot_its_collections_Async()
    {
        var defaultProfile = CgalProfileManifest.LoadDefault();
        var explicitProfile = defaultProfile with
        {
            ProfileId = "epick-explicit-test-v1",
            Declarations = Array.AsReadOnly(defaultProfile.Declarations
                .Where(static item => item.Id != "point-2-cartesian").ToArray()),
        };
        var file = WriteProfile(explicitProfile);
        try
        {
            var provider = CreateProvider(file, explicitProfile.ProfileId);
            var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);
            var manifest = await RenderAsync(plan.CSharpSources.Single(
                static item => item.RelativePath == "profile-manifest.json")).ConfigureAwait(false);
            NotSupportedException? mutationFailure = null;
            try
            {
                ((IList<string>)provider.Profile.SelectedHeaders)[0] = "mutated";
            }
            catch (NotSupportedException exception)
            {
                mutationFailure = exception;
            }

            await Assert.That(provider.Profile.ProfileId).IsEqualTo("epick-explicit-test-v1");
            await Assert.That(manifest).Contains("epick-explicit-test-v1");
            await Assert.That(provider.Inventory.Admitted.Count)
                .IsEqualTo(defaultProfile.Declarations.Count - 1);
            await Assert.That(plan.NativeExports).DoesNotContain("Cgal_Point2_Cartesian");
            await Assert.That(mutationFailure).IsNotNull();
            await Assert.That(CgalProfileManifest.LoadDefault().ProfileId).IsEqualTo("epick-windows-v1");
        }
        finally
        {
            File.Delete(file.FullName);
        }
    }

    /// <summary>
    /// Verifies real source evidence cannot silently admit a declaration without provider semantics.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_report_a_narrow_unsupported_declaration_Async()
    {
        var profile = CgalProfileManifest.LoadDefault();
        var unsupported = new CgalProfileDeclaration()
        {
            Id = "unsupported-point-capability",
            NativeSignature = "Kernel::Point_2::unsupported_capability() const",
            Header = "CGAL/Point_2.h",
            Kind = "method",
            Evidence = "class Point_2 :",
            IsRoot = true,
            Dependencies = ["point-2",],
        };
        var explicitProfile = profile with
        {
            ProfileId = "epick-unsupported-test-v1",
            Declarations = Array.AsReadOnly(profile.Declarations.Append(unsupported).ToArray()),
        };
        var file = WriteProfile(explicitProfile);
        try
        {
            var provider = CreateProvider(file, explicitProfile.ProfileId);
            var plan = await provider.CreatePlanAsync(CancellationToken.None).ConfigureAwait(false);

            await Assert.That(provider.Inventory.Candidates.Count).IsGreaterThan(profile.Declarations.Count + 1);
            await Assert.That(provider.Inventory.Unsupported.Select(static item => item.Id)).Contains(unsupported.Id);
            await Assert.That(provider.Inventory.Unsupported.Single(item => item.Id == unsupported.Id).Proof)
                .IsEqualTo("no-provider-semantic-projection");
            await Assert.That(plan.NativeExports).DoesNotContain("unsupported-point-capability");
        }
        finally
        {
            File.Delete(file.FullName);
        }
    }

    /// <summary>
    /// Verifies a known declaration identity cannot admit a different closed native signature.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_reject_a_mutated_supported_signature_Async()
    {
        var profile = CgalProfileManifest.LoadDefault();
        var declarations = profile.Declarations.Select(static item => item.Id == "point-2-x"
            ? item with { NativeSignature = "Kernel::Point_2::y() const", }
            : item).ToArray();
        var invalid = profile with
        {
            ProfileId = "epick-mutated-signature-test-v1",
            Declarations = Array.AsReadOnly(declarations),
        };
        var file = WriteProfile(invalid);
        InvalidOperationException? failure = null;
        try
        {
            _ = CreateProvider(file, invalid.ProfileId);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }
        finally
        {
            File.Delete(file.FullName);
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message).Contains("does not match its compiler-proved closed signature");
    }

    /// <summary>
    /// Verifies a finite profile cannot omit a declaration required by another candidate.
    /// </summary>
    /// <returns>A task that completes when assertions finish.</returns>
    [Test]
    public async Task Should_reject_a_missing_declaration_dependency_Async()
    {
        var profile = CgalProfileManifest.LoadDefault();
        var invalid = profile with
        {
            ProfileId = "epick-missing-dependency-test-v1",
            Declarations = Array.AsReadOnly(profile.Declarations.Where(static item => item.Id != "point-2").ToArray()),
        };
        var file = WriteProfile(invalid);
        InvalidOperationException? failure = null;
        try
        {
            _ = CreateProvider(file, invalid.ProfileId);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }
        finally
        {
            File.Delete(file.FullName);
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Message).Contains("requires missing declaration 'point-2'");
    }

    private static CgalGenerationProvider CreateProvider(
        FileInfo? profile = null,
        string? profileId = null,
        bool requireLockedToolchain = true,
        string? vcpkgRoot = null)
    {
        var root = vcpkgRoot ?? GetVcpkgRoot();
        return new(new()
        {
            VcpkgRoot = new(root),
            ProfileManifestFile = profile,
            ProfileId = profileId ?? CgalGenerationOptions.DefaultProfileId,
            RequireLockedToolchain = requireLockedToolchain,
            CSharpFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-managed")),
            CppFolder = new(Path.Combine(Path.GetTempPath(), "tedtoolkit-cgal-native")),
            CSharpNamespace = "TedToolkit.CppBindings.Cgal",
            NativeLibraryBaseName = "ted_toolkit_cpp_bindings_cgal",
            CppVersion = 20,
        });
    }

    private static string GetVcpkgRoot()
    {
        return Environment.GetEnvironmentVariable("VCPKG_ROOT") is { Length: > 0, } root ? root : @"C:\vcpkg";
    }

    private static string[] EnumerateInstalledHeaders()
    {
        var includeRoot = Path.Combine(GetVcpkgRoot(), "installed", "x64-windows", "include");
        return Directory.EnumerateFiles(Path.Combine(includeRoot, "CGAL"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(includeRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] EnumerateVcpkgPackageHeaders()
    {
        const string Triplet = "x64-windows";
        var files = Directory.EnumerateFiles(
                Path.Combine(GetVcpkgRoot(), "installed", "vcpkg", "info"),
                "cgal_*.list",
                SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith($"_{Triplet}.list", StringComparison.Ordinal))
            .ToArray();
        if (files.Length != 1)
        {
            throw new InvalidOperationException($"Expected one installed CGAL package list, found {files.Length}.");
        }

        const string Prefix = $"{Triplet}/include/";
        return File.ReadLines(files[0])
            .Select(static line => line.Replace('\\', '/'))
            .Where(static line => line.StartsWith(Prefix + "CGAL/", StringComparison.Ordinal)
                && !line.EndsWith('/'))
            .Select(static line => line[Prefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static FileInfo WriteProfile(CgalProfileManifest profile)
    {
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"tedtoolkit-cgal-{Guid.NewGuid():N}.json"));
        File.WriteAllText(file.FullName, JsonSerializer.Serialize(profile, ProfileJsonOptions));
        return file;
    }

    private static async Task<SortedDictionary<string, string>> RenderAsync(IEnumerable<GeneratedSource> sources)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            result.Add(source.RelativePath, await RenderAsync(source).ConfigureAwait(false));
        }

        return result;
    }

    private static async Task<string> RenderAsync(GeneratedSource source)
    {
        var writer = new StringWriter();
        await using (writer.ConfigureAwait(false))
        {
            await source.RenderAsync(writer, CancellationToken.None).ConfigureAwait(false);
            return writer.ToString();
        }
    }

    private static string Hash(IReadOnlyDictionary<string, string> sources)
    {
        var canonical = string.Join("\n", sources.Select(static item => item.Key + "\n" + item.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}