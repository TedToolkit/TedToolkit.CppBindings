// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>
/// Produces the deterministic, finite Manifold managed/native binding pair.
/// </summary>
public sealed class ManifoldGenerationProvider
{
    private static readonly string[] ExportNames =
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

    /// <summary>Gets the supported finite profile.</summary>
    public ManifoldProfile Profile { get; } = new();

    /// <summary>Creates a fresh immutable plan.</summary>
    public ManifoldGenerationPlan CreatePlan()
    {
        return CreatePlan(ResolveVcpkgRoot());
    }

    /// <summary>Creates a fresh immutable plan from the selected vcpkg installation.</summary>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked Manifold package.</param>
    /// <returns>The immutable generation plan.</returns>
    public ManifoldGenerationPlan CreatePlan(DirectoryInfo vcpkgRoot)
    {
        ArgumentNullException.ThrowIfNull(vcpkgRoot);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var sourceInventory = ManifoldHeaderDiscovery.Resolve(vcpkgRoot, Profile);
        var managedInventory = new[]
        {
            "Manifold", "Manifold.Create", "Manifold.Status", "Manifold.Boolean",
            "Manifold.Translate", "Manifold.NumTri", "Manifold.GetMesh", "ManifoldMeshData",
            "ManifoldOp", "ManifoldError",
        };
        var admitted = managedInventory.Select(static value => new { id = value, disposition = "admitted" }).ToArray();
        var sourceDeclarations = managedInventory.Select(static value => new
        {
            id = value,
            header = value.StartsWith("Manifold", StringComparison.Ordinal) ? "manifold/manifold.h" : "manifold/mesh.h",
            disposition = "finite-profile-candidate",
        }).ToArray();
        var unsupported = new[]
        {
            new { id = "callbacks", reason = "callbacks-outside-finite-profile" },
            new { id = "optional-mesh-properties", reason = "optional-properties-outside-finite-profile" },
            new { id = "manifoldc", reason = "alternate-c-api-outside-finite-profile" },
        };
        var layoutInventory = new[]
        {
            new { id = "ManifoldAdapter", size = 8, alignment = 8, proof = "compiler-static-assert" },
        };
        var ownershipInventory = new[]
        {
            new { id = "Manifold", ownership = "Owned", nativeStorage = "ManifoldAdapter" },
            new { id = "ManifoldMeshData", ownership = "managed-arrays", nativeStorage = "none" },
        };
        var managed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Manifold.Bindings.g.cs"] = ManifoldSourceRenderer.RenderManaged(Profile),
            ["admitted-inventory.json"] = JsonSerializer.Serialize(admitted, jsonOptions) + "\n",
            ["candidate-inventory.json"] = JsonSerializer.Serialize(sourceDeclarations, jsonOptions) + "\n",
            ["layout-inventory.json"] = JsonSerializer.Serialize(layoutInventory, jsonOptions) + "\n",
            ["managed-inventory.json"] = JsonSerializer.Serialize(managedInventory, jsonOptions) + "\n",
            ["ownership-inventory.json"] = JsonSerializer.Serialize(ownershipInventory, jsonOptions) + "\n",
            ["profile-manifest.json"] = JsonSerializer.Serialize(Profile, jsonOptions) + "\n",
            ["source-declaration-inventory.json"] = JsonSerializer.Serialize(sourceDeclarations, jsonOptions) + "\n",
            ["source-inventory.json"] = JsonSerializer.Serialize(sourceInventory.Select(static item => new
            {
                header = item.Header,
                disposition = item.Disposition,
            }), jsonOptions) + "\n",
            ["toolchain-inventory.json"] = JsonSerializer.Serialize(new
            {
                Profile.ManifoldVersion,
                Profile.Triplet,
                Profile.VcpkgBuiltinBaseline,
                Profile.CMake,
                Profile.Msvc,
            }, jsonOptions) + "\n",
            ["unsupported-inventory.json"] = JsonSerializer.Serialize(unsupported, jsonOptions) + "\n",
        };
        var native = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CMakeLists.txt"] = ManifoldSourceRenderer.RenderCMake(Profile),
            ["ManifoldProfileAdapter.cpp"] = ManifoldSourceRenderer.RenderNative(Profile, ExportNames),
            ["native-inventory.json"] = JsonSerializer.Serialize(ExportNames, jsonOptions) + "\n",
        };
        return new ManifoldGenerationPlan
        {
            ProfileId = Profile.ProfileId,
            ManagedSources = ManifoldGenerationPlan.Snapshot(managed),
            NativeSources = ManifoldGenerationPlan.Snapshot(native),
            NativeExports = Array.AsReadOnly((string[])ExportNames.Clone()),
        };
    }

    /// <summary>Writes one plan without retaining inputs or mutable plan state.</summary>
    public async Task GenerateAsync(DirectoryInfo outputRoot, CancellationToken cancellationToken)
    {
        await GenerateAsync(outputRoot, ResolveVcpkgRoot(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes one plan from the selected vcpkg installation without retaining mutable state.</summary>
    /// <param name="outputRoot">The generated output root.</param>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked Manifold package.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after all outputs are written.</returns>
    public async Task GenerateAsync(
        DirectoryInfo outputRoot,
        DirectoryInfo vcpkgRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(vcpkgRoot);
        var plan = CreatePlan(vcpkgRoot);
        await WriteAsync(new DirectoryInfo(Path.Combine(outputRoot.FullName, "csharp")), plan.ManagedSources, cancellationToken)
            .ConfigureAwait(false);
        await WriteAsync(new DirectoryInfo(Path.Combine(outputRoot.FullName, "cpp")), plan.NativeSources, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot.FullName, "generation-result.json"),
            JsonSerializer.Serialize(new
            {
                plan.ProfileId,
                ManagedArtifactCount = plan.ManagedSources.Count,
                NativeArtifactCount = plan.NativeSources.Count,
                NativeExportCount = plan.NativeExports.Count,
                Toolchain = new { Profile.CMake, Profile.Msvc, Profile.Triplet },
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n",
            cancellationToken).ConfigureAwait(false);
    }

    private static DirectoryInfo ResolveVcpkgRoot()
    {
        var path = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "VCPKG_ROOT is required when a vcpkg root is not supplied explicitly.");
        }

        return new DirectoryInfo(path);
    }

    private static async Task WriteAsync(
        DirectoryInfo root,
        IReadOnlyDictionary<string, string> sources,
        CancellationToken cancellationToken)
    {
        root.Create();
        foreach (var source in sources.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            await File.WriteAllTextAsync(Path.Combine(root.FullName, source.Key), source.Value, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}