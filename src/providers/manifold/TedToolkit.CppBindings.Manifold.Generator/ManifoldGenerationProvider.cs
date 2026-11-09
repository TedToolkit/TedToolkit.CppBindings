// -----------------------------------------------------------------------
// <copyright file="ManifoldGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Manifold.Generator;

/// <summary>Supplies the finite Manifold profile to the provider-neutral Shared semantic engine.</summary>
public sealed class ManifoldGenerationProvider : SemanticGenerationProvider
{
    private const string LinuxProfileId = "manifold-3.5.2-linux-x64-v1";

    private static readonly string[] Admitted =
    [
        "Manifold", "Manifold.Create", "Manifold.Status", "Manifold.Boolean",
        "Manifold.Translate", "Manifold.NumTri", "Manifold.GetMesh", "ManifoldMeshData",
        "ManifoldOp", "ManifoldError",
    ];

    private readonly INativeSourceProvider _nativeSource;

    private readonly bool _fromInstallation;

    /// <summary>Initializes a provider using the process <c>VCPKG_ROOT</c>.</summary>
    public ManifoldGenerationProvider()
        : this(ResolveVcpkgRoot())
    {
    }

    /// <summary>Initializes a provider from the selected vcpkg installation.</summary>
    /// <param name="vcpkgRoot">The vcpkg root that supplies the locked Manifold package.</param>
    public ManifoldGenerationProvider(in DirectoryPath vcpkgRoot)
        : this(vcpkgRoot, fromInstallation: false)
    {
    }

    private ManifoldGenerationProvider(in DirectoryPath root, bool fromInstallation)
        : base(ManifoldSemanticProfile.Create())
    {
        ArgumentNullException.ThrowIfNull(root.FullName, nameof(root));
        var fullRoot = root.GetFullPath();
        _fromInstallation = fromInstallation;
        _nativeSource = fromInstallation
            ? new ManifoldInstalledSourceProvider(fullRoot, Profile)
            : new ManifoldVcpkgSourceProvider(fullRoot, Profile);
    }

    /// <summary>Creates a provider from an installed Manifold CMake package.</summary>
    /// <param name="prefix">The installation prefix containing the public Manifold headers.</param>
    /// <returns>A provider that verifies the installed headers against the finite profile.</returns>
    public static ManifoldGenerationProvider FromInstallation(in DirectoryPath prefix)
    {
        return new(prefix, fromInstallation: true);
    }

    /// <summary>Gets the default Windows profile and common Manifold ABI facts.</summary>
    public ManifoldProfile Profile { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    /// <inheritdoc />
    public override bool SupportsRuntimeIdentifier(string runtimeIdentifier)
    {
        return runtimeIdentifier is "win-x64" || (_fromInstallation && runtimeIdentifier is "linux-x64");
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        return CreateProviderModelAsync("win-x64", null, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateProviderModelAsync(options.RuntimeIdentifier, options.NativeLibraryVersion, cancellationToken);
    }

    private Task<BindingProviderModel> CreateProviderModelAsync(
        string runtimeIdentifier,
        Version? nativeLibraryVersion,
        in CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SupportsRuntimeIdentifier(runtimeIdentifier))
        {
            throw new NotSupportedException(
                $"Manifold does not support generation target '{runtimeIdentifier}' with the selected native source.");
        }

        var linuxTarget = runtimeIdentifier is "linux-x64";
        ValidateNativeLibraryVersion(nativeLibraryVersion);
        var sourceInventory = ManifoldHeaderDiscovery.Classify(_nativeSource.Resolve(cancellationToken));
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
            JsonSource("profile-manifest.json", linuxTarget
                ? new
                {
                    ProfileId = LinuxProfileId,
                    RuntimeIdentifier = runtimeIdentifier,
                    Profile.ManifoldVersion,
                    Profile.NativeLibraryBaseName,
                    Profile.NativeOperationUnderlyingType,
                    Profile.NativeErrorUnderlyingType,
                    Profile.Operations,
                    Profile.Errors,
                }
                : Profile),
            JsonSource("source-declaration-inventory.json", sourceDeclarations),
            JsonSource("source-inventory.json", sourceInventory.Select(static item => new
            {
                header = item.Header,
                disposition = item.Disposition,
            })),
            JsonSource("toolchain-inventory.json", linuxTarget
                ? new { RuntimeIdentifier = runtimeIdentifier, Profile.ManifoldVersion }
                : new
                {
                    Profile.ManifoldVersion,
                    Profile.Triplet,
                    VcpkgBuiltinBaseline = _fromInstallation ? null : Profile.VcpkgBuiltinBaseline,
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
            ManifoldFiniteProfile.CreateNativeProject(
                Profile, nativeLibraryVersion, _fromInstallation, linuxTarget),
            [finiteApi,]));
    }

    private void ValidateNativeLibraryVersion(Version? requestedVersion)
    {
        if (requestedVersion is null
            || (Version.TryParse(Profile.ManifoldVersion, out var profileVersion)
                && requestedVersion == profileVersion))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Requested Manifold {requestedVersion} does not match finite profile '{Profile.ProfileId}' "
            + $"version {Profile.ManifoldVersion}.");
    }

    private static BindingSourceDefinition JsonSource(string path, object value)
    {
        var text = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        return new(path, (writer, token) => writer.WriteAsync(text.AsMemory(), token));
    }

    private static DirectoryPath ResolveVcpkgRoot()
    {
        var path = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "VCPKG_ROOT is required when a vcpkg root is not supplied explicitly.");
        }

        return new DirectoryPath(path).GetFullPath();
    }
}