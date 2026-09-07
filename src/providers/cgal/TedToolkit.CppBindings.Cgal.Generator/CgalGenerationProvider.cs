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
    private static readonly string[] Exports =
    [
        "Cgal_NativeError_Clear",
        "Cgal_Point2_Cartesian",
        "Cgal_Point2_SquaredDistance",
        "Cgal_Point3_Cartesian",
        "Cgal_Point3_SquaredDistance",
        "Cgal_Segment2_Intersection",
        "Cgal_Segment2_SquaredLength",
    ];

    private static readonly string[] ManagedFileInventory =
    [
        "Cgal.Generated.g.cs",
        "NativeApi.g.cs",
        "admitted-inventory.json",
        "candidate-inventory.json",
        "managed-inventory.json",
        "profile-manifest.json",
        "source-inventory.json",
        "unsupported-inventory.json",
    ];

    private static readonly string[] NativeFileInventory =
    [
        "CMakeLists.txt",
        "Cgal.Native.cpp",
        "NativeFunctionTable.cpp",
        "native-inventory.json",
    ];

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
        Profile = CgalProfileManifest.LoadDefault();
        ValidateOptions();
        Inventory = ResolveInventory();
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
        BindingSourceDefinition[] managedSources =
        {
            TextSource("Cgal.Generated.g.cs", CgalSourceRenderer.RenderManaged(_options.CSharpNamespace, Exports)),
            JsonSource(
                "profile-manifest.json",
                new
                {
                    Profile,
                    InstalledPublicHeaders = Inventory.Sources.Select(static item => item.Header),
                }),
            JsonSource("source-inventory.json", Inventory.Sources),
            JsonSource("candidate-inventory.json", Inventory.Candidates),
            JsonSource("admitted-inventory.json", Inventory.Admitted),
            JsonSource("unsupported-inventory.json", Inventory.Unsupported),
            JsonSource("managed-inventory.json", Inventory.ManagedFiles),
        };
        BindingSourceDefinition[] nativeSources =
        {
            TextSource("Cgal.Native.cpp", CgalSourceRenderer.RenderNative()),
            JsonSource("native-inventory.json", Inventory.NativeFiles),
        };
        var nativeProject = new BindingNativeProject(
            "CMakeLists.txt",
            (sources, writer, token) => writer.WriteAsync(
                CgalSourceRenderer.RenderCMake(_options.NativeLibraryBaseName, sources).AsMemory(), token));
        return Task.FromResult(new BindingProviderModel(
            [],
            [],
            CgalSemanticProfile.CreateEmissionProfile(_options.CSharpNamespace, _options.IsInternal),
            managedSources,
            nativeSources,
            Exports,
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
        if (!string.Equals(_options.ProfileId, CgalGenerationOptions.DefaultProfileId, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unknown finite CGAL profile '{_options.ProfileId}'.");
        }

        if (string.Equals(Profile.ProfileId, _options.ProfileId, StringComparison.Ordinal)
            && string.Equals(Profile.CgalVersion, "6.2", StringComparison.Ordinal)
            && string.Equals(Profile.Triplet, "x64-windows", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException("The embedded CGAL profile identity is inconsistent.");
    }

    private CgalGenerationInventory ResolveInventory()
    {
        var includeRoot = Path.Combine(_options.VcpkgRoot.FullName, "installed", Profile.Triplet, "include");
        var cgalRoot = Path.Combine(includeRoot, "CGAL");
        if (!Directory.Exists(cgalRoot))
        {
            throw new DirectoryNotFoundException($"CGAL public headers were not found beneath '{cgalRoot}'.");
        }

        var installed = Directory.EnumerateFiles(cgalRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(includeRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var locked = CgalProfileResources.LoadLockedHeaders();
        if (_options.RequireLockedHeaderInventory && !installed.SequenceEqual(locked, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The installed CGAL public-header inventory does not match the locked {Profile.CgalVersion} profile.");
        }

        var selected = Profile.SelectedHeaders.ToHashSet(StringComparer.Ordinal);
        foreach (var header in selected)
        {
            if (!installed.Contains(header, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Profile header '{header}' is absent from the installed CGAL package.");
            }
        }

        var candidates = Profile.Declarations.OrderBy(static item => item.Id, StringComparer.Ordinal).ToArray();
        var admitted = candidates.Where(static item => item.Disposition == "admitted").ToArray();
        var unsupported = candidates.Where(static item => item.Disposition == "unsupported").ToArray();
        if (admitted.Length + unsupported.Length != candidates.Length)
        {
            throw new InvalidOperationException("Every CGAL candidate must be admitted or unsupported.");
        }

        return new()
        {
            Sources = Array.AsReadOnly(installed.Select(header => new CgalSourceDisposition(
                header,
                selected.Contains(header) ? "profile-root" : "not-selected-by-finite-profile")).ToArray()),
            Candidates = Array.AsReadOnly(candidates),
            Admitted = Array.AsReadOnly(admitted),
            Unsupported = Array.AsReadOnly(unsupported),
            ManagedFiles = Array.AsReadOnly(ManagedFileInventory),
            NativeFiles = Array.AsReadOnly(NativeFileInventory),
        };
    }
}