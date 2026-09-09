// -----------------------------------------------------------------------
// <copyright file="BindingSemanticEngine.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.CppBindings.Generator.Generators;

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Applies provider-owned semantic rules through one deterministic, provider-neutral boundary.
/// </summary>
public sealed class BindingSemanticEngine
{
    private static readonly SemaphoreSlim ManagedEmissionSlots = new(2);

    private static readonly SemaphoreSlim EnumEmissionSlots = new(2);

    private readonly IReadOnlyList<IBindingTypeRule> _typeRules;

    private readonly Dictionary<string, IBindingEmitterPrimitive> _emitters;

    private readonly IBindingLayoutPolicy _layoutPolicy;

    private readonly IBindingTemplatePolicy _templatePolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingSemanticEngine"/> class.
    /// </summary>
    /// <param name="typeRules">Ordered type projection rules.</param>
    /// <param name="layoutPolicy">Native layout admission policy.</param>
    /// <param name="templatePolicy">Template admission policy.</param>
    /// <param name="emitters">Named provider emitter primitives.</param>
    public BindingSemanticEngine(
        IEnumerable<IBindingTypeRule> typeRules,
        IBindingLayoutPolicy layoutPolicy,
        IBindingTemplatePolicy templatePolicy,
        IEnumerable<IBindingEmitterPrimitive> emitters)
    {
        ArgumentNullException.ThrowIfNull(typeRules);
        ArgumentNullException.ThrowIfNull(layoutPolicy);
        ArgumentNullException.ThrowIfNull(templatePolicy);
        ArgumentNullException.ThrowIfNull(emitters);
        _typeRules = Array.AsReadOnly(typeRules.ToArray());
        _layoutPolicy = layoutPolicy;
        _templatePolicy = templatePolicy;
        var emitterArray = emitters.ToArray();
        if (emitterArray.Any(static emitter => emitter is null || string.IsNullOrWhiteSpace(emitter.Name)))
        {
            throw new ArgumentException("Emitter primitives must have non-empty names.", nameof(emitters));
        }

        try
        {
            _emitters = emitterArray.ToDictionary(static emitter => emitter.Name, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Emitter primitive names must be unique.", nameof(emitters), exception);
        }
    }

    /// <summary>
    /// Resolves a C++ type using the first matching provider rule.
    /// </summary>
    /// <param name="type">The native type descriptor.</param>
    /// <returns>The provider projection.</returns>
    /// <exception cref="InvalidOperationException">No provider rule recognizes the type.</exception>
    public BindingTypeProjection ResolveType(CppTypeDescriptor type)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(type.NativeName);
        ArgumentOutOfRangeException.ThrowIfNegative(type.PointerDepth);
        foreach (var rule in _typeRules)
        {
            if (rule.TryResolve(type, out var projection))
            {
                return projection ?? throw new InvalidOperationException("A matching type rule returned no projection.");
            }
        }

        throw new InvalidOperationException($"No type projection rule recognizes '{type.NativeName}'.");
    }

    /// <summary>
    /// Applies provider layout admission.
    /// </summary>
    /// <param name="layout">The compiler-proved layout.</param>
    /// <returns>The admission result.</returns>
    public BindingAdmission AdmitLayout(BindingLayoutDescriptor layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(layout.NativeTypeName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Alignment);
        return ValidateAdmission(_layoutPolicy.Admit(layout));
    }

    /// <summary>
    /// Applies provider template admission.
    /// </summary>
    /// <param name="template">The template descriptor.</param>
    /// <returns>The admission result.</returns>
    public BindingAdmission AdmitTemplate(BindingTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.FamilyName);
        ArgumentNullException.ThrowIfNull(template.Arguments);
        return ValidateAdmission(_templatePolicy.Admit(template));
    }

    /// <summary>
    /// Emits one provider primitive by its stable name.
    /// </summary>
    /// <param name="primitiveName">The registered primitive name.</param>
    /// <param name="value">The semantic value to emit.</param>
    /// <returns>The emitted text.</returns>
    /// <exception cref="KeyNotFoundException">The primitive name is not registered.</exception>
    /// <exception cref="InvalidOperationException">The registered primitive returned <see langword="null"/>.</exception>
    public string Emit(string primitiveName, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primitiveName);
        ArgumentNullException.ThrowIfNull(value);
        if (!_emitters.TryGetValue(primitiveName, out var emitter))
        {
            throw new KeyNotFoundException($"Emitter primitive '{primitiveName}' is not registered.");
        }

        return emitter.Emit(value) ?? throw new InvalidOperationException(
            $"Emitter primitive '{primitiveName}' returned null.");
    }

    /// <summary>
    /// Closes provider roots over their declared dependencies and constructs one normalized model.
    /// </summary>
    /// <param name="providerModel">The provider roots, policies, renderers, and native metadata.</param>
    /// <returns>The immutable normalized model used for both generated languages.</returns>
    public BindingSemanticModel CreateModel(BindingProviderModel providerModel)
    {
        ArgumentNullException.ThrowIfNull(providerModel);
        ValidateProviderInputs(providerModel);
        providerModel = new BindingSemanticGraphSnapshot().Create(providerModel);
        var candidates = CreateCandidateCatalog(providerModel.Declarations);
        var closure = CloseDependencies(candidates);
        var declarations = closure
            .Select(ValidateDeclaration)
            .OrderBy(static declaration => declaration.Type.NativeTypeName, StringComparer.Ordinal)
            .ToArray();
        var nativeExports = providerModel.NativeExports
            .Concat(declarations.SelectMany(static declaration => declaration.NativeExports))
            .ToArray();
        GenerationOutput.ValidateExports(nativeExports);
        Array.Sort(nativeExports, StringComparer.Ordinal);
        return new(providerModel, declarations, nativeExports);
    }

    /// <summary>
    /// Constructs matching managed and native source inventories from one normalized model.
    /// </summary>
    /// <param name="providerModel">The provider roots, policies, renderers, and native metadata.</param>
    /// <returns>The completed plan consumed by the existing generation pipeline.</returns>
    public GenerationPlan CreatePlan(BindingProviderModel providerModel)
    {
        var model = CreateModel(providerModel);
        var slots = new ReadOnlyDictionary<string, int>(model.NativeExports
            .Select(static (export, index) => (export, index))
            .ToDictionary(static item => item.export, static item => item.index, StringComparer.Ordinal));
        var managed = CreateManagedSources(model, slots);
        managed.AddRange(CreateEnumSources(model));
        managed.AddRange(model.Provider.ManagedSources.Select(ToGeneratedSource));
        var native = CreateNativeSources(model);
        native.AddRange(model.Provider.NativeSources.Select(ToGeneratedSource));
        AddNativeProject(model.Provider.NativeProject, native);
        return new(managed, native, model.NativeExports);
    }

    private static void ValidateProviderInputs(BindingProviderModel providerModel)
    {
        if (providerModel.Declarations.Any(static declaration => declaration is null)
            || providerModel.Enums.Any(static declaration => declaration is null)
            || providerModel.ManagedSources.Any(static source => source is null || source.RenderAsync is null)
            || providerModel.NativeSources.Any(static source => source is null || source.RenderAsync is null)
            || providerModel.EmissionProfile.NativeExceptionProjections.Any(static projection => projection is null))
        {
            throw new ArgumentException("Provider model collections cannot contain null values.", nameof(providerModel));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerModel.EmissionProfile.CSharpNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModel.EmissionProfile.ManagedNativeErrorProjection);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModel.EmissionProfile.NativeErrorClearExport);
        ArgumentNullException.ThrowIfNull(providerModel.EmissionProfile.NativePreamble);
        ArgumentNullException.ThrowIfNull(providerModel.EmissionProfile.NativeErrorPreamble);
        ArgumentNullException.ThrowIfNull(providerModel.EmissionProfile.NativePreambleHeaders);
        ArgumentNullException.ThrowIfNull(providerModel.EmissionProfile.NativeExceptionProjections);

        foreach (var projection in providerModel.EmissionProfile.NativeExceptionProjections)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projection.CppType);
            ArgumentException.ThrowIfNullOrWhiteSpace(projection.NativeTypeExpression);
            ArgumentException.ThrowIfNullOrWhiteSpace(projection.MessageExpression);

            if (projection.Code is <= 8 or >= 255)
            {
                throw new ArgumentException(
                    $"Provider native exception '{projection.CppType}' must use a code from 9 through 254.",
                    nameof(providerModel));
            }

            if (BindingNativeErrorCatchEmitter.IsSharedExceptionType(projection.CppType))
            {
                throw new ArgumentException(
                    $"Provider native exception '{projection.CppType}' is already projected by Shared.",
                    nameof(providerModel));
            }
        }

        if (providerModel.NativeProject is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(providerModel.NativeProject.RenderAsync);
    }

    private static Dictionary<string, BindingDeclaration> CreateCandidateCatalog(
        IReadOnlyList<BindingDeclaration> declarations)
    {
        var candidates = new Dictionary<string, BindingDeclaration>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Type.NativeName);
            if (!candidates.TryAdd(declaration.Type.NativeName, declaration))
            {
                throw new InvalidOperationException(
                    $"Duplicate provider declaration '{declaration.Type.NativeName}'.");
            }
        }

        return candidates;
    }

    private static BindingDeclaration[] CloseDependencies(
        IReadOnlyDictionary<string, BindingDeclaration> candidates)
    {
        var closure = new Dictionary<string, BindingDeclaration>(StringComparer.Ordinal);
        var pending = new Queue<BindingDeclaration>(candidates.Values
            .Where(static declaration => declaration.IsRoot)
            .OrderBy(static declaration => declaration.Type.NativeName, StringComparer.Ordinal));
        while (pending.TryDequeue(out var declaration))
        {
            if (!closure.TryAdd(declaration.Type.NativeName, declaration))
            {
                continue;
            }

            foreach (var dependencyName in declaration.Dependencies.Order(StringComparer.Ordinal))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
                if (!candidates.TryGetValue(dependencyName, out var dependency))
                {
                    throw new InvalidOperationException(
                        $"Declaration '{declaration.Type.NativeName}' requires missing dependency '{dependencyName}'.");
                }

                pending.Enqueue(dependency);
            }
        }

        return closure.Values.ToArray();
    }

    private BindingSemanticDeclaration ValidateDeclaration(BindingDeclaration declaration)
    {
        if (!string.Equals(
                declaration.Type.NativeName,
                declaration.Layout.NativeTypeName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Declaration '{declaration.Type.NativeName}' has layout for '{declaration.Layout.NativeTypeName}'.");
        }

        var type = ResolveType(declaration.Type);
        var layout = AdmitLayout(declaration.Layout);
        if (!layout.IsAdmitted)
        {
            throw new InvalidOperationException(
                $"Declaration '{declaration.Type.NativeName}' was rejected by layout policy: {layout.Reason}");
        }

        if (declaration.Template is { } template)
        {
            var templateAdmission = AdmitTemplate(template);
            if (!templateAdmission.IsAdmitted)
            {
                throw new InvalidOperationException(
                    $"Declaration '{declaration.Type.NativeName}' was rejected by template policy: {templateAdmission.Reason}");
            }
        }

        ValidateRecord(declaration.Record);

        return new(declaration, type);
    }

    private void ValidateRecord(RecordModel record)
    {
        ArgumentNullException.ThrowIfNull(record.Type);
        ArgumentNullException.ThrowIfNull(record.FieldModels);
        ArgumentNullException.ThrowIfNull(record.MethodModels);
        ArgumentNullException.ThrowIfNull(record.Bases);
        if (record.ObjectKind is NativeObjectKind.Unknown)
        {
            throw new InvalidOperationException(
                $"Declaration '{record.Type.CppTypeName}' has no proved lifetime or ownership classification.");
        }

        if (record.ObjectKind is NativeObjectKind.IntrusiveHandle && !record.UsesIntrusiveReferenceCounting)
        {
            throw new InvalidOperationException(
                $"Declaration '{record.Type.CppTypeName}' uses intrusive ownership without proved reference-count lifetime.");
        }

        ValidateType(record.Type, $"declaration '{record.Type.CppTypeName}'");
        foreach (var field in record.FieldModels)
        {
            ArgumentNullException.ThrowIfNull(field);
            if (field.Offset < 0 || field.Size <= 0 || field.Alignment <= 0)
            {
                throw new InvalidOperationException(
                    $"Field '{record.Type.CppTypeName}.{field.Name}' has invalid compiler layout evidence.");
            }

            if (field.ManagedReadOnlyPropertyName is not null
                && (!field.IsManagedStoragePrivate
                    || string.IsNullOrWhiteSpace(field.ManagedReadOnlyPropertyName)
                    || string.Equals(field.Name, field.ManagedReadOnlyPropertyName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Field '{record.Type.CppTypeName}.{field.Name}' has an invalid managed read-only projection.");
            }

            ValidateType(field.Type, $"field '{record.Type.CppTypeName}.{field.Name}'");
        }

        foreach (var method in record.MethodModels)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentException.ThrowIfNullOrWhiteSpace(method.NativeExportName);
            ValidateType(method.ReturnType, $"return of '{record.Type.CppTypeName}.{method.MethodName}'");
            foreach (var parameter in method.Parameters)
            {
                ArgumentNullException.ThrowIfNull(parameter);
                ValidateType(
                    parameter.Type,
                    $"parameter '{parameter.Name}' of '{record.Type.CppTypeName}.{method.MethodName}'");
            }
        }

        foreach (var relation in record.Bases)
        {
            ArgumentNullException.ThrowIfNull(relation);
            ArgumentNullException.ThrowIfNull(relation.Base);
            ValidateType(relation.Base.Type, $"base of '{record.Type.CppTypeName}'");
        }
    }

    private void ValidateType(TypeModel type, string owner)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(type.CppTypeName);
        ArgumentNullException.ThrowIfNull(type.Transport);
        ArgumentNullException.ThrowIfNull(type.Transport.Indirections);
        try
        {
            _ = ResolveType(new(
                type.CppTypeName,
                type.Transport.ValueIsConst,
                type.Transport.Indirections.Count(static value =>
                    value.Kind is TypeIndirectionKind.PointerIndirection)));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Unable to project {owner}: {exception.Message}", exception);
        }
    }

    private List<GeneratedSource> CreateManagedSources(
        BindingSemanticModel model,
        IReadOnlyDictionary<string, int> slots)
    {
        var catalog = model.Declarations.ToDictionary(
            static declaration => declaration.Type.NativeTypeName,
            static declaration => declaration.Source.Record,
            StringComparer.Ordinal);
        return model.Declarations
            .GroupBy(static declaration => declaration.ManagedSourceGroup, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var declarations = group.OrderBy(
                        static declaration => declaration.Type.NativeTypeName,
                        StringComparer.Ordinal)
                    .ToArray();
                return new GeneratedSource(
                    Emit(model.Provider.ManagedSourceStemEmitter, group.Key) + ".g.cs",
                    (writer, token) => RenderManagedGroupAsync(
                        declarations,
                        catalog,
                        model.Provider.EmissionProfile,
                        slots,
                        writer,
                        token));
            })
            .ToList();
    }

    private List<GeneratedSource> CreateNativeSources(BindingSemanticModel model)
    {
        return model.Declarations
            .OrderBy(static declaration => declaration.Type.ManagedTypeName, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.Type.NativeTypeName, StringComparer.Ordinal)
            .Select(declaration => new GeneratedSource(
                Emit(model.Provider.NativeSourceStemEmitter, declaration.Type.NativeTypeName) + ".cpp",
                async (writer, token) =>
                {
                    var code = await new BindingNativeEmitter(
                            declaration.Source.Record,
                            model.Provider.EmissionProfile)
                        .GenerateAsync(token).ConfigureAwait(false);
                    await writer.WriteAsync(code.AsMemory(), token).ConfigureAwait(false);
                }))
            .ToList();
    }

    private static async Task RenderManagedGroupAsync(
        BindingSemanticDeclaration[] declarations,
        IReadOnlyDictionary<string, RecordModel> catalog,
        BindingEmissionProfile emissionProfile,
        IReadOnlyDictionary<string, int> slots,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        await ManagedEmissionSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var index = 0; index < declarations.Length; index++)
            {
                var declaration = declarations[index];
                var code = await new BindingManagedEmitter(
                        declaration.Source.Record,
                        emissionProfile,
                        catalog,
                        slots,
                        index is 0)
                    .GenerateAsync(cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(code.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = ManagedEmissionSlots.Release();
        }
    }

    private List<GeneratedSource> CreateEnumSources(BindingSemanticModel model)
    {
        return model.Provider.Enums
            .OrderBy(static declaration => declaration.Name, StringComparer.Ordinal)
            .Select(declaration => new GeneratedSource(
                Emit(model.Provider.ManagedSourceStemEmitter, declaration.Name) + ".g.cs",
                (writer, token) => RenderEnumAsync(
                    declaration,
                    model.Provider.EmissionProfile.CSharpNamespace,
                    writer,
                    token)))
            .ToList();
    }

    private static async Task RenderEnumAsync(
        EnumModel declaration,
        string cSharpNamespace,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        await EnumEmissionSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var code = await new BindingEnumEmitter(declaration, cSharpNamespace)
                .GenerateAsync(cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(code.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = EnumEmissionSlots.Release();
        }
    }

    private static GeneratedSource ToGeneratedSource(BindingSourceDefinition source)
    {
        return new(source.RelativePath, source.RenderAsync);
    }

    private static void AddNativeProject(
        BindingNativeProject? nativeProject,
        List<GeneratedSource> nativeSources)
    {
        if (nativeProject is null)
        {
            return;
        }

        var projectSources = nativeSources
            .Select(static source => source.RelativePath)
            .Where(static path => path.EndsWith(".cpp", StringComparison.Ordinal))
            .Append(NativeFunctionTableGenerator.FileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        nativeSources.Add(new(
            nativeProject.RelativePath,
            (writer, token) => nativeProject.RenderAsync(projectSources, writer, token)));
    }

    private static BindingAdmission ValidateAdmission(BindingAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if ((admission.IsAdmitted && string.IsNullOrWhiteSpace(admission.Reason))
            || (!admission.IsAdmitted && !string.IsNullOrWhiteSpace(admission.Reason)))
        {
            return admission;
        }

        throw new InvalidOperationException(
            "An admitted result cannot have a reason, and a rejected result must have one.");
    }
}