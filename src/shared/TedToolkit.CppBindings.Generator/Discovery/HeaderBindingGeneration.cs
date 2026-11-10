// -----------------------------------------------------------------------
// <copyright file="HeaderBindingGeneration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Generator.Semantics;

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
                includeDirectories: options.IncludeDirectories.Select(static path => QuotePath(Path.GetFullPath(path.FullName))),
                linkLibraries: options.LinkLibraries.Select(QuotePath))));
        return engine.CreatePlan(AddHeaders(options, model, cancellationToken));
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
        if (options.Headers.Count == 0)
        {
            return model;
        }

        if (options.RuntimeIdentifier != "win-x64")
        {
            throw new NotSupportedException("Shared header discovery currently requires the win-x64 MSVC ABI.");
        }

        var headers = options.Headers
            .OrderBy(static header => header.Path.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static header => Path.GetFullPath(header.Path.FullName), GetWrapperName, StringComparer.OrdinalIgnoreCase);
        string[] arguments =
        [
            "-x", "c++", "--target=x86_64-pc-windows-msvc", "-std=c++" + options.CppVersion,
            .. options.IncludeDirectories.Select(static directory => "-I" + Path.GetFullPath(directory.FullName)),
            .. options.CompileDefinitions.Select(static definition => "-D" + definition),
        ];
        var includes = string.Join("\n", headers.Keys.Select(static header => "#include " + QuotePath(header)));
        var records = new HeaderRecordDiscovery(model.Declarations);
        var functions = HeaderCompilerDiscovery.Discover(headers, includes, arguments, records, cancellationToken);
        if (functions.Count == 0)
        {
            return model;
        }

        var declarations = records.CompleteDeclarations(HeaderGroup);
        ValidateWrapperNames(functions, declarations);
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
            []);
        return new(
            declarations, model.Enums, model.EmissionProfile, model.ManagedSources, model.NativeSources,
            model.NativeExports, model.ManagedSourceStemEmitter, model.NativeSourceStemEmitter, model.NativeProject,
            model.FiniteProfileApis) { HeaderApi = (api, profile), };
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

    private static void ValidateWrapperNames(
        IReadOnlyDictionary<string, BindingFreeFunctionDefinition> functions,
        BindingDeclaration[] declarations)
    {
        var typeNames = declarations.SelectMany(static declaration =>
            new[] { declaration.Record.Type.CSharpTypeName, declaration.Record.Type.CSharpInterfaceName, })
            .ToHashSet(StringComparer.Ordinal);
        var collision = functions.Values.FirstOrDefault(function => typeNames.Contains(function.ContainingType));
        if (collision is null)
        {
            return;
        }

        throw new InvalidOperationException($"Wrapper name '{collision.ContainingType}' conflicts with a generated object type.");
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