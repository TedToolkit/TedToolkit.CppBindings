// -----------------------------------------------------------------------
// <copyright file="FclGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Produces the deterministic finite FCL managed/native binding pair.</summary>
public sealed class FclGenerationProvider
{
    private static readonly string[] NativeFunctions =
    [
        "Fcl_NativeError_Clear",
        "Fcl_Model_Destroy",
        "Fcl_Model_Create",
        "Fcl_ContinuousCollision_Query",
        "Fcl_Lifetime_Reset",
        "Fcl_Lifetime_CreateCount",
        "Fcl_Lifetime_DestroyCount",
    ];

    /// <summary>Gets the finite profile.</summary>
    public FclProfile Profile { get; } = new();

    /// <summary>Creates a fresh immutable plan.</summary>
    public FclGenerationPlan CreatePlan()
    {
        return CreatePlan(ResolveVcpkgRoot());
    }

    /// <summary>Creates a fresh immutable plan from the selected vcpkg installation.</summary>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked FCL package.</param>
    /// <returns>The immutable generation plan.</returns>
    public FclGenerationPlan CreatePlan(DirectoryInfo vcpkgRoot)
    {
        ArgumentNullException.ThrowIfNull(vcpkgRoot);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var sourceInventory = FclHeaderDiscovery.Resolve(vcpkgRoot, Profile);
        var admitted = new[]
        {
            "FclVector3", "FclBvhReturnCode", "FclBvhModel.Create", "FclModelBuildResult",
            "FclContinuousCollision.Query", "FclContinuousCollisionResult",
        };
        var managed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Fcl.Bindings.g.cs"] = FclSourceRenderer.RenderManaged(Profile),
            ["admitted-inventory.json"] = JsonSerializer.Serialize(admitted, options) + "\n",
            ["candidate-inventory.json"] = JsonSerializer.Serialize(admitted.Select(static id => new
            {
                id,
                disposition = "finite-profile-candidate",
            }), options) + "\n",
            ["layout-inventory.json"] = JsonSerializer.Serialize(new[]
            {
                new { id = "FclVector3", size = 24, alignment = 8, proof = "compiler-static-assert" },
                new { id = "FclBvhModelAdapter", size = 8, alignment = 8, proof = "compiler-static-assert" },
            }, options) + "\n",
            ["managed-inventory.json"] = JsonSerializer.Serialize(admitted, options) + "\n",
            ["ownership-inventory.json"] = JsonSerializer.Serialize(new[]
            {
                new { id = "FclBvhModel", ownership = "Owned", nativeStorage = "FclBvhModelAdapter" },
                new { id = "FclVector3", ownership = "value", nativeStorage = "FclVector3Transport" },
            }, options) + "\n",
            ["profile-manifest.json"] = JsonSerializer.Serialize(Profile, options) + "\n",
            ["source-declaration-inventory.json"] = JsonSerializer.Serialize(admitted.Select(static id => new
            {
                id,
                header = "fcl/fcl.h",
                disposition = "finite-profile-candidate",
            }), options) + "\n",
            ["source-inventory.json"] = JsonSerializer.Serialize(sourceInventory.Select(static item => new
            {
                header = item.Header,
                disposition = item.Disposition,
            }), options) + "\n",
            ["toolchain-inventory.json"] = JsonSerializer.Serialize(Profile.Versions, options) + "\n",
            ["unsupported-inventory.json"] = JsonSerializer.Serialize(new[]
            {
                new { id = "callbacks", reason = "callbacks-outside-finite-profile" },
                new { id = "contact-transforms", reason = "contact-transforms-outside-finite-profile" },
                new { id = "request-controls", reason = "ignored-by-selected-obbrss-dispatch" },
                new { id = "reverse-mesh-conversion", reason = "outside-finite-profile" },
            }, options) + "\n",
        };
        var native = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CMakeLists.txt"] = FclSourceRenderer.RenderCMake(Profile),
            ["FclProfileAdapter.cpp"] = FclSourceRenderer.RenderNative(NativeFunctions),
            ["native-inventory.json"] = JsonSerializer.Serialize(NativeFunctions, options) + "\n",
        };
        return new FclGenerationPlan
        {
            ProfileId = Profile.ProfileId,
            ManagedSources = FclGenerationPlan.Snapshot(managed),
            NativeSources = FclGenerationPlan.Snapshot(native),
            NativeFunctions = Array.AsReadOnly((string[])NativeFunctions.Clone()),
        };
    }

    /// <summary>Writes one plan without retaining mutable state.</summary>
    public async Task GenerateAsync(DirectoryInfo outputRoot, CancellationToken cancellationToken)
    {
        await GenerateAsync(outputRoot, ResolveVcpkgRoot(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes one plan from the selected vcpkg installation without retaining mutable state.</summary>
    /// <param name="outputRoot">The generated output root.</param>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked FCL package.</param>
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
        await File.WriteAllTextAsync(Path.Combine(outputRoot.FullName, "generation-result.json"),
            JsonSerializer.Serialize(new
            {
                plan.ProfileId,
                ManagedArtifactCount = plan.ManagedSources.Count,
                NativeArtifactCount = plan.NativeSources.Count,
                NativeFunctionCount = plan.NativeFunctions.Count,
                Toolchain = Profile.Versions,
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n", cancellationToken).ConfigureAwait(false);
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
