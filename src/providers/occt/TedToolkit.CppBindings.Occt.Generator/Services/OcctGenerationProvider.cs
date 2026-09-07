// -----------------------------------------------------------------------
// <copyright file="OcctGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Services.Rules;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Supplies OCCT roots, policies, metadata, and renderers to Shared plan construction.
/// </summary>
internal sealed class OcctGenerationProvider : SemanticGenerationProvider
{
    private readonly IOptions<OcctGenerationOptions> _options;

    private readonly VcpkgDefaultTripletResolver _tripletResolver = new();

    private readonly VcpkgEnvironment _environment = new();

    private readonly RecordModelManager _records;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcctGenerationProvider"/> class.
    /// </summary>
    /// <param name="options">The OCCT configuration for this run.</param>
    internal OcctGenerationProvider(OcctGenerationOptions options)
        : base(OcctSemanticProfile.Create())
    {
        _options = Microsoft.Extensions.Options.Options.Create(options);
        _records = new(
            _options,
            new Resolver([new HandleTypeRule(), new Utf8StringTypeRule(),]),
            _tripletResolver,
            _environment);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules { get; } = Array.AsReadOnly(new[] { typeof(OcctCompilerProbeModule), });

    /// <summary>
    /// Gets a parsing stage over the same private model used to prepare the plan.
    /// </summary>
    internal OcctParseModule ParseModule
    {
        get
        {
            return new(_records, _options, _tripletResolver, _environment);
        }
    }

    /// <summary>
    /// Gets a native fact preparation stage over the same private model.
    /// </summary>
    internal OcctCompilerProbeModule ProbeModule
    {
        get
        {
            return new(_records, _options, _tripletResolver, _environment);
        }
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = _records.RecordModels.ToArray();
        var catalog = records.ToDictionary(static record => record.Type.CppTypeName, StringComparer.Ordinal);
        var declarations = records.Select(record => new BindingDeclaration(
            record,
            record.TemplateProjection?.FamilyName ?? record.Type.CSharpTypeName,
            record.IsPubliclyAccessible,
            record.NativeDependencyRecords
                .Where(dependency => catalog.ContainsKey(dependency.Type.CppTypeName))
                .Select(static dependency => dependency.Type.CppTypeName))).ToArray();
        var managed = new List<BindingSourceDefinition>();
        var admissionReport = "Target: win-x64; supported sequential native alignments: 1, 2, 4, 8.\n"
            + string.Join("\n", _records.UnsupportedDeclarations) + "\n";
        managed.Add(new BindingSourceDefinition("unsupported-declarations.txt",
            (writer, token) => writer.WriteAsync(admissionReport.AsMemory(), token)));
        var layouts = JsonSerializer.Serialize(new
        {
            Namespace = _options.Value.CSharpNamespace,
            Records = records.OrderBy(static record => record.Type.CppTypeName, StringComparer.Ordinal).Select(static record => new
            {
                NativeType = record.Type.CppTypeName,
                ManagedType = record.Type.CSharpTypeName,
                record.Size,
                record.Alignment,
                IsGeneric = record.TemplateProjection is not null,
            }),
        });
        managed.Add(new BindingSourceDefinition("native-layouts.json",
            (writer, token) => writer.WriteAsync(layouts.AsMemory(), token)));
        BindingSourceDefinition[] native =
        [
            new(
            NativeErrorSupportGenerator.HeaderFileName,
            static (writer, token) => writer.WriteAsync(NativeErrorSupportGenerator.GenerateHeader().AsMemory(), token)),
            new(
            NativeErrorSupportGenerator.SourceFileName,
            static (writer, token) => writer.WriteAsync(NativeErrorSupportGenerator.GenerateSource().AsMemory(), token)),
        ];
        var nativeProject = new BindingNativeProject(
            NativeProjectGenerator.FileName,
            (sources, writer, token) => writer.WriteAsync(NativeProjectGenerator.Generate(
                sources, _options.Value.NativeLibraryBaseName, _options.Value.CppVersion).AsMemory(), token));
        return Task.FromResult(new BindingProviderModel(
            declarations,
            _records.EnumModels,
            OcctSemanticProfile.CreateEmissionProfile(_options.Value.CSharpNamespace, _options.Value.IsInternal),
            managed,
            native,
            ["NativeError_Clear",],
            OcctSemanticProfile.ManagedSourceStem,
            OcctSemanticProfile.NativeSourceStem,
            nativeProject));
    }
}