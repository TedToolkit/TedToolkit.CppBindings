// -----------------------------------------------------------------------
// <copyright file="CgalGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Supplies the finite CGAL profile to the provider-neutral Shared semantic engine.
/// </summary>
public sealed class CgalGenerationProvider : SemanticGenerationProvider
{
    private readonly CgalGenerationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CgalGenerationProvider"/> class.
    /// </summary>
    /// <param name="options">The finite profile and vcpkg location.</param>
    public CgalGenerationProvider(CgalGenerationOptions options)
        : base(CgalSemanticProfile.Create())
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Profile = options.ProfileManifestFile is null
            ? CgalProfileManifest.LoadDefault()
            : CgalProfileManifest.Load(options.ProfileManifestFile);
        ValidateOptions();
        Inventory = CgalProfileDiscovery.Resolve(options, Profile);
    }

    /// <summary>
    /// Gets the resolved default profile.
    /// </summary>
    public CgalProfileManifest Profile { get; }

    /// <summary>
    /// Gets the deterministic resolved inventory.
    /// </summary>
    public CgalGenerationInventory Inventory { get; }

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var managedSources = new List<BindingSourceDefinition>()
        {
            JsonSource(
                "profile-manifest.json",
                new
                {
                    Profile,
                    InstalledPublicHeaders = Inventory.Sources.Select(static item => item.Header),
                }),
            JsonSource("source-inventory.json", Inventory.Sources),
            JsonSource("source-declaration-inventory.json", Inventory.SourceDeclarations),
            JsonSource("candidate-inventory.json", Inventory.Candidates),
            JsonSource("admitted-inventory.json", Inventory.Admitted),
            JsonSource("unsupported-inventory.json", Inventory.Unsupported),
            JsonSource("managed-inventory.json", Inventory.ManagedArtifacts),
            JsonSource("toolchain-inventory.json", Inventory.Toolchain),
        };
        if (Inventory.Admitted.Any(static item => item.Id == "intersection-segment-2"))
        {
            managedSources.Add(TextSource(
                "Cgal.ResultProjection.g.cs",
                CgalSourceRenderer.RenderManagedResultProjection(_options.CSharpNamespace)));
        }

        BindingSourceDefinition[] nativeSources =
        {
            TextSource("CgalProfileAdapter.hpp", CgalSourceRenderer.RenderAdapter(Profile)),
            TextSource("CgalNativeError.hpp", CgalSourceRenderer.RenderNativeErrorHeader()),
            TextSource("CgalNativeError.cpp", CgalSourceRenderer.RenderNativeErrorSource()),
            JsonSource("native-inventory.json", Inventory.NativeArtifacts),
        };
        var nativeProject = BindingCMakeProjectEmitter.CreateNativeProject(new(
            "TedToolkitCppBindingsCgal",
            _options.NativeLibraryBaseName,
            20,
            [new("CGAL", "CONFIG REQUIRED"),],
            compileDefinitions: ["CGAL_DEBUG",],
            includeDirectories: ["\"${CMAKE_CURRENT_SOURCE_DIR}\"",],
            linkLibraries: ["CGAL::CGAL",]));
        return Task.FromResult(new BindingProviderModel(
            CgalSemanticCatalog.CreateDeclarations(Inventory.Admitted),
            [],
            CgalSemanticProfile.CreateEmissionProfile(_options.CSharpNamespace, _options.IsInternal),
            managedSources,
            nativeSources,
            ["Cgal_NativeError_Clear",],
            CgalSemanticProfile.ManagedSourceStem,
            CgalSemanticProfile.NativeSourceStem,
            nativeProject));
    }

    private static BindingSourceDefinition JsonSource(string path, object value)
    {
        var text = JsonSerializer.Serialize(value, CgalProfileResources.JsonOptions) + "\n";
        return TextSource(path, text);
    }

    private static BindingSourceDefinition TextSource(string path, string text)
    {
        return new(path, (writer, token) => writer.WriteAsync(text.AsMemory(), token));
    }

    private void ValidateOptions()
    {
        if (!string.Equals(_options.ProfileId, Profile.ProfileId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Requested profile '{_options.ProfileId}' does not match manifest '{Profile.ProfileId}'.");
        }

        if (_options.ProfileManifestFile is not null
            || (string.Equals(Profile.CgalVersion, "6.2", StringComparison.Ordinal)
                && string.Equals(Profile.Triplet, "x64-windows", StringComparison.Ordinal)
                && string.Equals(
                    Profile.VcpkgBuiltinBaseline,
                    "30ef65cad98f08e7197c9a1656fbd871bcb72f2d",
                    StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidOperationException("The embedded CGAL profile identity is inconsistent.");
    }
}