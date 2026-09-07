// -----------------------------------------------------------------------
// <copyright file="BindingProviderModel.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Supplies provider roots, supplemental emitters, and native metadata for Shared normalization.
/// </summary>
public sealed class BindingProviderModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingProviderModel"/> class with copied inputs.
    /// </summary>
    /// <param name="declarations">Candidate declarations including roots and their dependencies.</param>
    /// <param name="enums">Normalized enum declarations emitted by Shared.</param>
    /// <param name="emissionProfile">Finite provider policy consumed by Shared's paired emitters.</param>
    /// <param name="managedSources">Provider-specific supplemental managed sources.</param>
    /// <param name="nativeSources">Provider-specific supplemental native sources.</param>
    /// <param name="nativeExports">Provider-wide native exports not owned by one declaration.</param>
    /// <param name="managedSourceStemEmitter">The registered emitter used for managed source stems.</param>
    /// <param name="nativeSourceStemEmitter">The registered emitter used for native source stems.</param>
    /// <param name="nativeProject">The native build-file renderer, when required.</param>
    public BindingProviderModel(
        IEnumerable<BindingDeclaration> declarations,
        IEnumerable<EnumModel> enums,
        BindingEmissionProfile emissionProfile,
        IEnumerable<BindingSourceDefinition> managedSources,
        IEnumerable<BindingSourceDefinition> nativeSources,
        IEnumerable<string> nativeExports,
        string managedSourceStemEmitter,
        string nativeSourceStemEmitter,
        BindingNativeProject? nativeProject)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(enums);
        ArgumentNullException.ThrowIfNull(emissionProfile);
        ArgumentNullException.ThrowIfNull(managedSources);
        ArgumentNullException.ThrowIfNull(nativeSources);
        ArgumentNullException.ThrowIfNull(nativeExports);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedSourceStemEmitter);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSourceStemEmitter);
        Declarations = Array.AsReadOnly(declarations.ToArray());
        Enums = Array.AsReadOnly(enums.ToArray());
        EmissionProfile = emissionProfile.Snapshot();
        ManagedSources = Array.AsReadOnly(managedSources.ToArray());
        NativeSources = Array.AsReadOnly(nativeSources.ToArray());
        NativeExports = Array.AsReadOnly(nativeExports.ToArray());
        ManagedSourceStemEmitter = managedSourceStemEmitter;
        NativeSourceStemEmitter = nativeSourceStemEmitter;
        NativeProject = nativeProject;
    }

    /// <summary>
    /// Gets candidate declarations including roots and their dependencies.
    /// </summary>
    public IReadOnlyList<BindingDeclaration> Declarations { get; }

    /// <summary>
    /// Gets normalized enum declarations emitted by Shared.
    /// </summary>
    public IReadOnlyList<EnumModel> Enums { get; }

    /// <summary>
    /// Gets finite provider policy consumed by Shared's paired emitters.
    /// </summary>
    public BindingEmissionProfile EmissionProfile { get; }

    /// <summary>
    /// Gets provider-specific supplemental managed sources.
    /// </summary>
    public IReadOnlyList<BindingSourceDefinition> ManagedSources { get; }

    /// <summary>
    /// Gets provider-specific supplemental native sources.
    /// </summary>
    public IReadOnlyList<BindingSourceDefinition> NativeSources { get; }

    /// <summary>
    /// Gets provider-wide native exports not owned by one declaration.
    /// </summary>
    public IReadOnlyList<string> NativeExports { get; }

    /// <summary>
    /// Gets the registered emitter used for managed source stems.
    /// </summary>
    public string ManagedSourceStemEmitter { get; }

    /// <summary>
    /// Gets the registered emitter used for native source stems.
    /// </summary>
    public string NativeSourceStemEmitter { get; }

    /// <summary>
    /// Gets the native build-file renderer, when required.
    /// </summary>
    public BindingNativeProject? NativeProject { get; }
}