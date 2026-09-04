// -----------------------------------------------------------------------
// <copyright file="OcctGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;
using TedToolkit.CppBindings.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Adapts the private OCCT model and rendering policy to the neutral completed-plan boundary.
/// </summary>
internal sealed class OcctGenerationProvider : IGenerationProvider, IDisposable
{
    private readonly IOptions<OcctGenerationOptions> _options;

    private readonly VcpkgDefaultTripletResolver _tripletResolver = new();

    private readonly VcpkgEnvironment _environment = new();

    private readonly RecordModelManager _records;

    private readonly GeneratorService _generators;

    private readonly SemaphoreSlim _recordRenderers = new(2);

    private readonly SemaphoreSlim _enumRenderers = new(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="OcctGenerationProvider"/> class.
    /// </summary>
    /// <param name="options">The OCCT configuration for this run.</param>
    internal OcctGenerationProvider(OcctGenerationOptions options)
    {
        _options = Microsoft.Extensions.Options.Options.Create(options);
        _records = new(
            _options,
            new Resolver([new HandleTypeRule(), new Utf8StringTypeRule(),]),
            _tripletResolver,
            _environment);
        _generators = new(_options);
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> PreparationModules { get; } = Array.AsReadOnly(new[] { typeof(OcctCompilerProbeModule), });

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
    public Task<GenerationPlan> CreatePlanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = _records.RecordModels.ToArray();
        var catalog = records.ToDictionary(static record => record.Type.CppTypeName, StringComparer.Ordinal);
        var exports = NativeExportInventory.GetExports(records);
        var slots = exports.Select(static (export, index) => (export, index))
            .ToDictionary(static item => item.export, static item => item.index, StringComparer.Ordinal);
        var families = records.GroupBy(static record => record.TemplateProjection is { } projection
                ? "family:" + projection.FamilyName
                : "record:" + record.Type.CppTypeName,
            StringComparer.Ordinal);
        var managed = new List<GeneratedSource>();
        foreach (var family in families)
        {
            var members = family.OrderBy(static record => record.Type.CppTypeName, StringComparer.Ordinal).ToArray();
            var stem = members[0].TemplateProjection?.FamilyName ?? members[0].Type.CSharpTypeName;
            managed.Add(new GeneratedSource(
                stem.ToGeneratedFileStem() + ".g.cs",
                (writer, token) => RenderFamilyAsync(members, catalog, slots, writer, token)));
        }

        managed.AddRange(_records.EnumModels.Select(model => new GeneratedSource(
            model.Name.ToGeneratedFileStem() + ".g.cs",
            (writer, token) => RenderEnumAsync(model, writer, token))));
        var native = records.OrderBy(static record => record.Type.CSharpTypeName, StringComparer.Ordinal)
            .Select(record => new GeneratedSource(
                record.Type.CppTypeName.ToGeneratedTypeName().ToGeneratedFileStem(80) + ".cpp",
                async (writer, token) =>
                {
                    var code = await _generators.GenerateCpp(record).GenerateAsync(token).ConfigureAwait(false);
                    await writer.WriteAsync(code.AsMemory(), token).ConfigureAwait(false);
                }))
            .ToList();
        native.Add(new GeneratedSource(
            NativeErrorSupportGenerator.HeaderFileName,
            static (writer, token) => writer.WriteAsync(NativeErrorSupportGenerator.GenerateHeader().AsMemory(), token)));
        native.Add(new GeneratedSource(
            NativeErrorSupportGenerator.SourceFileName,
            static (writer, token) => writer.WriteAsync(NativeErrorSupportGenerator.GenerateSource().AsMemory(), token)));
        var projectSources = native.Select(static source => source.RelativePath)
            .Where(static path => path.EndsWith(".cpp", StringComparison.Ordinal))
            .Append("NativeFunctionTable.cpp")
            .ToArray();
        native.Add(new GeneratedSource(
            NativeProjectGenerator.FileName,
            (writer, token) => writer.WriteAsync(NativeProjectGenerator.Generate(
                projectSources, _options.Value.NativeLibraryBaseName, _options.Value.CppVersion).AsMemory(), token)));
        return Task.FromResult(new GenerationPlan(managed, native, exports));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _recordRenderers.Dispose();
        _enumRenderers.Dispose();
    }

    private async Task RenderFamilyAsync(
        RecordModel[] family,
        IReadOnlyDictionary<string, RecordModel> catalog,
        IReadOnlyDictionary<string, int> slots,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        await _recordRenderers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var index = 0; index < family.Length; index++)
            {
                var code = await _generators.GenerateCSharp(family[index], catalog, slots, generateRepresentation: index is 0)
                    .GenerateAsync(cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(code.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _recordRenderers.Release();
        }
    }

    private async Task RenderEnumAsync(EnumModel model, TextWriter writer, CancellationToken cancellationToken)
    {
        await _enumRenderers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var code = await _generators.GenerateCSharp(model).GenerateAsync(cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(code.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _enumRenderers.Release();
        }
    }
}