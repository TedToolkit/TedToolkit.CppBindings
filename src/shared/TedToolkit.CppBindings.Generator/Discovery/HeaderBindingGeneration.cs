// -----------------------------------------------------------------------
// <copyright file="HeaderBindingGeneration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Adds shared header discovery to the same semantic plan as existing object declarations.
/// </summary>
internal static class HeaderBindingGeneration
{
    private const string HeaderGroup = "HeaderLayouts";

    /// <summary>
    /// Creates the default shared plan for a header-only configuration.
    /// </summary>
    /// <param name="options">The shared generation options.</param>
    /// <param name="cancellationToken">Cancellation for discovery.</param>
    /// <returns>The paired managed and native generation plan.</returns>
    internal static GenerationPlan CreatePlan(GenerationOptions options, in CancellationToken cancellationToken)
    {
        var policy = new HeaderLayoutPolicy();
        var engine = new BindingSemanticEngine([], policy, policy, []);
        var model = new BindingProviderModel(
            [], [], CreateEmissionProfile(options.CSharpNamespace, options.IsInternal, "TedToolkit_Header_"),
            [], [], [], HeaderGroup, HeaderGroup,
            BindingCMakeProjectEmitter.CreateNativeProject(new(
                "HeaderBindings",
                options.GetNativeLibraryBaseName(),
                options.CppVersion,
                [],
                compileDefinitions: options.CompileDefinitions.Select(QuoteCMakeArgument),
                compileOptions: ["/EHs", "/EHc-",],
                includeDirectories: GetIncludeDirectories(options).Select(QuotePath),
                linkLibraries: GetLinkLibraries(options).Select(QuotePath))
            { LinkDirectories = GetLinkDirectories(options).Select(QuotePath).ToArray(), }));
        return engine.CreatePlanWithHeaders(model, options, cancellationToken);
    }

    /// <summary>
    /// Adds the configured headers to an existing semantic input before Shared assigns any slots.
    /// </summary>
    /// <param name="options">The compiler and input header options.</param>
    /// <param name="model">The existing semantic input.</param>
    /// <param name="cancellationToken">Cancellation for discovery.</param>
    /// <returns>The merged semantic input.</returns>
    /// <exception cref="NotSupportedException">The target ABI has no shared compiler proof.</exception>
    internal static BindingProviderModel AddHeaders(
        GenerationOptions options,
        BindingProviderModel model,
        in CancellationToken cancellationToken)
    {
        var headers = ResolveHeaders(options);
        var includeDirectories = GetIncludeDirectories(options).ToArray();
        GenerationOutput.ValidateInputPaths(options, headers.Keys.Concat(includeDirectories)
            .Concat(GetLinkDirectories(options)).Concat(GetLinkLibraries(options).Where(Path.IsPathFullyQualified)));
        if (headers.Count == 0)
        {
            return model;
        }

        if (options.RuntimeIdentifier != "win-x64")
        {
            throw new NotSupportedException("Shared header discovery currently requires the win-x64 MSVC ABI.");
        }

        string[] arguments =
        [
            "-x", "c++", "--target=x86_64-pc-windows-msvc", "-std=c++" + options.CppVersion,
            .. includeDirectories.Select(static directory => "-I" + directory),
            .. options.CompileDefinitions.Select(static definition => "-D" + definition),
        ];
        var includes = string.Join("\n", headers.Keys.Select(static header => "#include " + QuotePath(header)));
        var records = new HeaderRecordDiscovery(model.Declarations);
        var functions = HeaderCompilerDiscovery.Discover(headers, includes, arguments, records, options, cancellationToken);
        if (functions.Count == 0)
        {
            return model;
        }

        var declarations = records.CompleteDeclarations(HeaderGroup);
        ValidateWrapperNames(functions.Values, declarations, model);
        var exportPrefix = ChooseExportPrefix(functions, model);
        var profile = CreateEmissionProfile(model.EmissionProfile.CSharpNamespace, model.EmissionProfile.IsInternal, exportPrefix);
        var operations = functions.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value with { NativeExport = exportPrefix + "Call_" + pair.Key, })
            .ToArray();
        var api = new BindingFiniteProfileApi(
            "Headers/Functions.g.cs",
            "Headers/Functions.cpp",
            [], [], [], [], [],
            operations,
            "#include <cstdlib>\n#include <cstring>\n#include <memory>\n#include <new>\n#include <stdexcept>\n"
            + "#include <typeinfo>\n#include <type_traits>\n#include <cstddef>\n"
            + includes + "\n\n" + records.GetNativeLayoutAssertions(),
            "An external C linkage function threw an unknown native exception.",
            operations.Select(static operation => operation.NativeExport).Prepend(profile.NativeErrorClearExport).ToArray(),
            []) { HeaderProfile = profile, };
        return new(
            declarations, model.Enums, model.EmissionProfile, model.ManagedSources, model.NativeSources,
            model.NativeExports, model.ManagedSourceStemEmitter, model.NativeSourceStemEmitter, model.NativeProject,
            model.FiniteProfileApis.Append(api));
    }

    private static BindingEmissionProfile CreateEmissionProfile(string managedNamespace, bool isInternal, string exportPrefix)
    {
        return new()
        {
            CSharpNamespace = managedNamespace,
            IsInternal = isInternal,
            ManagedNativeErrorProjection = "global::TedToolkit.CppBindings.NativeErrorProjection",
            NativeErrorClearExport = exportPrefix + "ClearError",
            NativeErrorSetter = exportPrefix + "SetError",
            NativePreamble = "",
            NativeErrorPreamble = "",
        };
    }

    private static string GetWrapperName(BindingHeader header)
    {
        var stem = Path.GetFileNameWithoutExtension(header.Path.FullName);
        var generated = string.Concat(stem.Select(static character =>
            SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_')) + "Wrapper";
        if (!SyntaxFacts.IsIdentifierStartCharacter(generated[0]))
        {
            generated = "_" + generated;
        }

        var name = header.WrapperClassName ?? generated;
        if (HeaderCompilerDiscovery.IsManagedIdentifier(name) && name != "NativeApi")
        {
            return name;
        }

        throw new ArgumentException($"'{name}' is not an available wrapper class name for '{header.Path.FullName}'.");
    }

    private static Dictionary<string, string> ResolveHeaders(GenerationOptions options)
    {
        var nativeHeaders = options.NativeInputs is { } inputs
            ? inputs.PublicHeaders.Select(header => new BindingHeader(new FilePath(Path.GetFullPath(header, inputs.IncludeRoot.FullName))))
            : [];
        return nativeHeaders.Concat(options.Headers)
            .GroupBy(static header => Path.GetFullPath(header.Path.FullName), StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => GetWrapperName(group.Last()), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetIncludeDirectories(GenerationOptions options)
    {
        var nativeDirectories = options.NativeInputs is { } inputs
            ? inputs.DependencyIncludeDirectories.Prepend(inputs.IncludeRoot)
            : [];
        return nativeDirectories.Concat(options.IncludeDirectories)
            .Select(static path => Path.GetFullPath(path.FullName))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetLinkDirectories(GenerationOptions options)
    {
        return options.NativeInputs?.DependencyLibraryDirectories.Select(static path => path.FullName) ?? [];
    }

    private static IEnumerable<string> GetLinkLibraries(GenerationOptions options)
    {
        return (options.NativeInputs?.DependencyLibraries.Select(static path => path.FullName) ?? [])
            .Concat(options.LinkLibraries);
    }

    private static void ValidateWrapperNames(
        IEnumerable<BindingFreeFunctionDefinition> functions,
        BindingDeclaration[] declarations,
        BindingProviderModel model)
    {
        var typeNames = declarations.SelectMany(static declaration =>
            new[] { declaration.Record.Type.CSharpTypeName, declaration.Record.Type.CSharpInterfaceName, })
            .Concat(model.Enums.Select(static value => value.Name))
            .Concat(model.FiniteProfileApis.SelectMany(BindingFiniteProfileManagedComposer.GetDeclaredTypeNames))
            .ToHashSet(StringComparer.Ordinal);
        var methods = new HashSet<(string ContainingType, string MethodName)>();
        foreach (var function in functions)
        {
            if (typeNames.Contains(function.ContainingType))
            {
                throw new InvalidOperationException($"Wrapper name '{function.ContainingType}' conflicts with a generated type.");
            }

            if (function.MethodName == function.ContainingType || !methods.Add((function.ContainingType, function.MethodName)))
            {
                throw new InvalidOperationException(
                    $"Method '{function.MethodName}' is not unique in wrapper '{function.ContainingType}'.");
            }
        }
    }

    private static string ChooseExportPrefix(
        IReadOnlyDictionary<string, BindingFreeFunctionDefinition> functions,
        BindingProviderModel model)
    {
        var occupied = functions.Keys.Concat(model.NativeExports)
            .Concat(model.Declarations.SelectMany(static declaration => declaration.NativeExports))
            .Concat(model.FiniteProfileApis.SelectMany(static api => api.NativeExportOrder)).ToArray();
        var prefix = "TedToolkit_FreeFunction_";
        while (occupied.Any(symbol => symbol.StartsWith(prefix, StringComparison.Ordinal)))
        {
            prefix += "_";
        }

        return prefix;
    }

    private static string QuotePath(string path)
    {
        return QuoteCMakeArgument(path.Replace('\\', '/'));
    }

    private static string QuoteCMakeArgument(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private sealed class HeaderLayoutPolicy : IBindingLayoutPolicy, IBindingTemplatePolicy
    {
        public BindingAdmission Admit(BindingLayoutDescriptor layout)
        {
            return BindingAdmission.Reject("The layout was not proved by shared header discovery.");
        }

        public BindingAdmission Admit(BindingTemplateDescriptor template)
        {
            return BindingAdmission.Reject("The template was not projected by a semantic provider.");
        }
    }
}