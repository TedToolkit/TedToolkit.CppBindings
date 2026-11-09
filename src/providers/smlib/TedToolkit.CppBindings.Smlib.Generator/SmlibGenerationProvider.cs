// -----------------------------------------------------------------------
// <copyright file="SmlibGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Smlib.Generator;

/// <summary>
/// Supplies a finite SMLib solid-modeling profile from the USD BRep source tree.
/// </summary>
public sealed class SmlibGenerationProvider : SemanticGenerationProvider
{
    /// <summary>
    /// The native module basename shared by generated bindings and runtime packages.
    /// </summary>
    public const string NativeLibraryBaseName = "ted_toolkit_cpp_bindings_smlib";

    private const string ClearError = "Smlib_NativeError_Clear";

    private const string Destroy = "Smlib_Brep_Destroy";

    private const string CreateBox = "Smlib_Brep_CreateBox";

    private const string ComputeVolume = "Smlib_Brep_ComputeVolume";

    private static readonly Version SourceVersion = new(1, 0, 1);

    private readonly NativeSourceInputs _inputs;

    /// <summary>
    /// Initializes a provider using an official USD BRep 1.0.1 source checkout.
    /// </summary>
    /// <param name="sourceRoot">The checkout root containing the VERSION and source entries.</param>
    /// <exception cref="InvalidOperationException">The checkout has a different version.</exception>
    public SmlibGenerationProvider(in DirectoryPath sourceRoot)
        : base(new([], new FinitePolicy(), new FinitePolicy(), []))
    {
        _inputs = NativeSources.FromSourceDirectory(
            sourceRoot,
            "source/SM_API/inc",
            ["SmApiPrimitives.h", "SmApiQueries.h",],
            [sourceRoot / "source" / "SMLib" / "inc",]);
        var version = (_inputs.SourceDirectory!.Value / new FileName("VERSION")).ReadAllText().Trim();
        if (string.Equals(version, SourceVersion.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"The SMLib profile requires USD BRep {SourceVersion}. Found '{version}'.");
    }

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.Empty<Type>();

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateProviderModel());
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (options.NativeLibraryVersion is { } requested && requested != SourceVersion)
        {
            throw new InvalidOperationException($"Requested SMLib {requested} does not match USD BRep {SourceVersion}.");
        }

        if (!string.Equals(options.NativeLibraryBaseName, NativeLibraryBaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The SMLib profile requires native library basename '{NativeLibraryBaseName}'.");
        }

        return Task.FromResult(CreateProviderModel());
    }

    private BindingProviderModel CreateProviderModel()
    {
        var profile = new BindingEmissionProfile()
        {
            CSharpNamespace = "TedToolkit.CppBindings.Smlib",
            ManagedNativeErrorProjection = "global::TedToolkit.CppBindings.NativeErrorProjection",
            NativeErrorClearExport = ClearError,
            NativePreamble = "",
            NativeErrorPreamble = "",
        };
        return new(
            [], [], profile, [], [], [], "managed-source-stem", "native-source-stem",
            CreateNativeProject(), [CreateApi(),]);
    }

    private BindingNativeProject CreateNativeProject()
    {
        var definition = new BindingCMakeProjectDefinition(
            NativeLibraryBaseName, NativeLibraryBaseName, 17, [new("Threads", "REQUIRED"),],
            compileDefinitions: ["SM_API_EXPORTS",],
            includeDirectories: _inputs.DependencyIncludeDirectories.Prepend(_inputs.IncludeRoot)
                .Select(static path => CMakePath(path.FullName)));
        using var module = typeof(SmlibGenerationProvider).Assembly.GetManifestResourceStream("SmlibSources.cmake")!;
        using var reader = new StreamReader(module);
        var source = CMakePath(_inputs.SourceDirectory!.Value.FullName);
        var buildSources = $"\nset(smlib_source {source})\n{reader.ReadToEnd()}";
        return new("CMakeLists.txt", (files, writer, token) => writer.WriteAsync(
            (BindingCMakeProjectEmitter.Render(definition, files) + buildSources).AsMemory(), token));
    }

    private static string CMakePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var delimiter = "=";
        while (normalized.Contains($"]{delimiter}]", StringComparison.Ordinal))
        {
            delimiter += "=";
        }

        return $"[{delimiter}[{normalized}]{delimiter}]";
    }

    private BindingFiniteProfileApi CreateApi()
    {
        var entryIncludes = string.Concat(_inputs.PublicHeaders.Select(static header => $"#include <{header}>\n"));
        return new(
            "Smlib.Bindings.g.cs", "Smlib.Bindings.cpp", [], [],
            [new("Brep", "SmlibBrep", 8, 8, Destroy),],
            [], [],
            [
                new BindingOwnedOperationDefinition(
                    "Solids", "CreateBox", "Brep", [],
                    [
                        new("x", "double", "double"),
                        new("y", "double", "double"),
                        new("z", "double", "double"),
                        new("length", "double", "double"),
                        new("width", "double", "double"),
                        new("height", "double", "double"),
                    ],
                    CreateBox,
                    """
                    const std::lock_guard guard(SmlibMutex);
                    SmBrep* native = nullptr;
                    const auto status = SmApiCreateBox(SmVector3d(x, y, z), length, width, height, native);
                    std::unique_ptr<SmBrep> owner(native);
                    CheckStatus(status, "SmApiCreateBox");
                    if (!owner) throw std::runtime_error("SmApiCreateBox returned an empty solid");
                    new (result) SmlibBrep(std::move(owner));
                    """),
                new BindingScalarOperationDefinition(
                    "BrepExtensions", "ComputeVolume", "double", "double", "0.0",
                    [new("brep", "Brep", IsExtensionReceiver: true),],
                    [new("relativeAccuracy", "double", "double"),],
                    ComputeVolume,
                    """
                    const std::lock_guard guard(SmlibMutex);
                    double volume = 0.0;
                    CheckStatus(SmApiBrepComputeVolume(brep->value.get(), relativeAccuracy, volume),
                        "SmApiBrepComputeVolume");
                    return volume;
                    """),
            ],
            entryIncludes + """
            #include <SmBrep.h>
            #include <SmMessages.h>
            #include <SmVector3d.h>
            #include <memory>
            #include <mutex>
            #include <stdexcept>
            #include <string>
            #include <utility>

            // SM_API uses a process-global, unsynchronized context.
            static std::mutex SmlibMutex;

            struct SmlibBrep
            {
                std::unique_ptr<SmBrep> value;

                explicit SmlibBrep(std::unique_ptr<SmBrep> owner) noexcept : value(std::move(owner)) {}

                ~SmlibBrep() noexcept
                {
                    const std::lock_guard guard(SmlibMutex);
                    value.reset();
                }
            };

            static_assert(sizeof(SmlibBrep) == 8 && alignof(SmlibBrep) == 8);

            static void CheckStatus(SmApiStatus status, const char* operation)
            {
                if (status != SM_SUCCESS)
                    throw std::runtime_error(std::string(operation) + " returned " + std::to_string(status));
            }
            """,
            "Unknown SMLib native failure",
            [ClearError, Destroy, CreateBox, ComputeVolume,],
            []);
    }

    private sealed class FinitePolicy : IBindingLayoutPolicy, IBindingTemplatePolicy
    {
        public BindingAdmission Admit(BindingLayoutDescriptor layout)
        {
            return BindingAdmission.Reject("No declaration layouts.");
        }

        public BindingAdmission Admit(BindingTemplateDescriptor template)
        {
            return BindingAdmission.Reject("No template declarations.");
        }
    }
}