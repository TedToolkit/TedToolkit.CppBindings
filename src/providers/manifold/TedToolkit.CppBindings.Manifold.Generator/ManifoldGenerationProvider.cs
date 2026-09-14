// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Supplies the finite Manifold profile to the provider-neutral Shared semantic engine.</summary>
public sealed class ManifoldGenerationProvider : SemanticGenerationProvider
{
    private static readonly string[] Admitted =
    [
        "Manifold", "Manifold.Create", "Manifold.Status", "Manifold.Boolean",
        "Manifold.Translate", "Manifold.NumTri", "Manifold.GetMesh", "ManifoldMeshData",
        "ManifoldOp", "ManifoldError",
    ];

    private readonly DirectoryInfo _vcpkgRoot;

    /// <summary>Initializes a provider using the process <c>VCPKG_ROOT</c>.</summary>
    public ManifoldGenerationProvider()
        : this(ResolveVcpkgRoot())
    {
    }

    /// <summary>Initializes a provider from the selected vcpkg installation.</summary>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked Manifold package.</param>
    public ManifoldGenerationProvider(DirectoryInfo vcpkgRoot)
        : base(ManifoldSemanticProfile.Create())
    {
        ArgumentNullException.ThrowIfNull(vcpkgRoot);
        _vcpkgRoot = vcpkgRoot;
    }

    /// <summary>Gets the finite profile.</summary>
    public ManifoldProfile Profile { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceInventory = ManifoldHeaderDiscovery.Resolve(_vcpkgRoot, Profile);
        var sourceDeclarations = Admitted.Select(static value => new
        {
            id = value,
            header = value.StartsWith("Manifold", StringComparison.Ordinal)
                ? "manifold/manifold.h"
                : "manifold/mesh.h",
            disposition = "finite-profile-candidate",
        }).ToArray();
        BindingSourceDefinition[] managedSources =
        [
            JsonSource("admitted-inventory.json", Admitted.Select(static value => new
            {
                id = value,
                disposition = "admitted",
            })),
            JsonSource("candidate-inventory.json", sourceDeclarations),
            JsonSource("layout-inventory.json", new[]
            {
                new { id = "ManifoldAdapter", size = 8, alignment = 8, proof = "compiler-static-assert" },
            }),
            JsonSource("managed-inventory.json", Admitted),
            JsonSource("ownership-inventory.json", new[]
            {
                new { id = "Manifold", ownership = "Owned", nativeStorage = "ManifoldAdapter" },
                new { id = "ManifoldMeshData", ownership = "managed-arrays", nativeStorage = "none" },
            }),
            JsonSource("profile-manifest.json", Profile),
            JsonSource("source-declaration-inventory.json", sourceDeclarations),
            JsonSource("source-inventory.json", sourceInventory.Select(static item => new
            {
                header = item.Header,
                disposition = item.Disposition,
            })),
            JsonSource("toolchain-inventory.json", new
            {
                Profile.ManifoldVersion,
                Profile.Triplet,
                Profile.VcpkgBuiltinBaseline,
                Profile.CMake,
                Profile.Msvc,
            }),
            JsonSource("unsupported-inventory.json", new[]
            {
                new { id = "callbacks", reason = "callbacks-outside-finite-profile" },
                new { id = "optional-mesh-properties", reason = "optional-properties-outside-finite-profile" },
                new { id = "manifoldc", reason = "alternate-c-api-outside-finite-profile" },
            }),
        ];
        var finiteApi = ManifoldFiniteProfile.Create(Profile);
        BindingSourceDefinition[] nativeSources =
        [
            JsonSource("native-inventory.json", finiteApi.NativeExportOrder),
        ];
        return Task.FromResult(new BindingProviderModel(
            [],
            [ManifoldFiniteProfile.CreateOperationEnum(Profile),],
            ManifoldSemanticProfile.CreateEmissionProfile(),
            managedSources,
            nativeSources,
            [],
            ManifoldSemanticProfile.ManagedSourceStem,
            ManifoldSemanticProfile.NativeSourceStem,
            ManifoldFiniteProfile.CreateNativeProject(Profile),
            [finiteApi,]));
    }

    private static BindingSourceDefinition JsonSource(string path, object value)
    {
        var text = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        return new(path, (writer, token) => writer.WriteAsync(text.AsMemory(), token));
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
}