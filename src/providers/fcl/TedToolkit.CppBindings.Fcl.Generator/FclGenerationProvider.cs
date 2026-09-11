// -----------------------------------------------------------------------
// <copyright file="FclGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Fcl.Generator;

/// <summary>Supplies the finite FCL profile to the provider-neutral Shared semantic engine.</summary>
public sealed class FclGenerationProvider : SemanticGenerationProvider
{
    private static readonly string[] Admitted =
    [
        "FclVector3", "FclBvhReturnCode", "FclBvhModel.Create", "FclModelBuildResult",
        "FclContinuousCollision.Query", "FclContinuousCollisionResult",
    ];

    private readonly DirectoryInfo _vcpkgRoot;

    /// <summary>Initializes a provider using the process <c>VCPKG_ROOT</c>.</summary>
    public FclGenerationProvider()
        : this(ResolveVcpkgRoot())
    {
    }

    /// <summary>Initializes a provider from the selected vcpkg installation.</summary>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked FCL package.</param>
    public FclGenerationProvider(DirectoryInfo vcpkgRoot)
        : base(FclSemanticProfile.Create())
    {
        ArgumentNullException.ThrowIfNull(vcpkgRoot);
        _vcpkgRoot = vcpkgRoot;
    }

    /// <summary>Gets the finite profile.</summary>
    public FclProfile Profile { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceInventory = FclHeaderDiscovery.Resolve(_vcpkgRoot, Profile);
        BindingSourceDefinition[] managedSources =
        [
            JsonSource("admitted-inventory.json", Admitted),
            JsonSource("candidate-inventory.json", Admitted.Select(static id => new
            {
                id,
                disposition = "finite-profile-candidate",
            })),
            JsonSource("layout-inventory.json", new[]
            {
                new { id = "FclVector3", size = 24, alignment = 8, proof = "compiler-static-assert" },
                new { id = "FclBvhModelAdapter", size = 8, alignment = 8, proof = "compiler-static-assert" },
            }),
            JsonSource("managed-inventory.json", Admitted),
            JsonSource("ownership-inventory.json", new[]
            {
                new { id = "FclBvhModel", ownership = "Owned", nativeStorage = "FclBvhModelAdapter" },
                new { id = "FclVector3", ownership = "value", nativeStorage = "FclVector3Transport" },
            }),
            JsonSource("profile-manifest.json", Profile),
            JsonSource("source-declaration-inventory.json", Admitted.Select(static id => new
            {
                id,
                header = "fcl/fcl.h",
                disposition = "finite-profile-candidate",
            })),
            JsonSource("source-inventory.json", sourceInventory.Select(static item => new
            {
                header = item.Header,
                disposition = item.Disposition,
            })),
            JsonSource("toolchain-inventory.json", Profile.Versions),
            JsonSource("unsupported-inventory.json", new[]
            {
                new { id = "callbacks", reason = "callbacks-outside-finite-profile" },
                new { id = "contact-transforms", reason = "contact-transforms-outside-finite-profile" },
                new { id = "request-controls", reason = "ignored-by-selected-obbrss-dispatch" },
                new { id = "reverse-mesh-conversion", reason = "outside-finite-profile" },
            }),
        ];
        var finiteApi = FclFiniteProfile.Create(Profile);
        var emissionProfile = FclSemanticProfile.CreateEmissionProfile();
        BindingSourceDefinition[] nativeSources =
        [
            JsonSource("native-inventory.json", finiteApi.NativeExportOrder),
        ];
        return Task.FromResult(new BindingProviderModel(
            [],
            [],
            emissionProfile,
            managedSources,
            nativeSources,
            [],
            FclSemanticProfile.ManagedSourceStem,
            FclSemanticProfile.NativeSourceStem,
            FclFiniteProfile.CreateNativeProject(Profile),
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