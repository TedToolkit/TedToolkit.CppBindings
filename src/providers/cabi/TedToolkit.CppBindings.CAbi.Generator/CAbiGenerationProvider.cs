// -----------------------------------------------------------------------
// <copyright file="CAbiGenerationProvider.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.CppBindings.Generator;
using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.CAbi.Generator;

/// <summary>
/// Generates static bindings for external C linkage functions declared in selected headers.
/// </summary>
public sealed class CAbiGenerationProvider : SemanticGenerationProvider
{
    private static readonly NoRecordPolicy RecordPolicy = new();

    private readonly CAbiGenerationOptions _options;

    /// <summary>
    /// Initializes a generator for the selected headers and native dependencies.
    /// </summary>
    /// <param name="options">The generation options.</param>
    public CAbiGenerationProvider(CAbiGenerationOptions options)
        : base(new BindingSemanticEngine([], RecordPolicy, RecordPolicy, []))
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options with
        {
            Headers = Array.AsReadOnly(options.Headers.ToArray()),
            IncludeDirectories = Array.AsReadOnly(options.IncludeDirectories.ToArray()),
            CompileDefinitions = Array.AsReadOnly(options.CompileDefinitions.ToArray()),
            LinkLibraries = Array.AsReadOnly(options.LinkLibraries.ToArray()),
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<Type> PreparationModules
    {
        get
        {
            return [];
        }
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(CancellationToken cancellationToken)
    {
        return CreateProviderModelAsync(_options, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<BindingProviderModel> CreateProviderModelAsync(
        in GenerationOptions options,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RuntimeIdentifier != "win-x64" || options.NativeLibraryVersion is not null)
        {
            throw new NotSupportedException("C linkage generation supports win-x64 with caller-selected native inputs.");
        }

        var headers = _options.Headers
            .Select(static header => (Path: Path.GetFullPath(header.Path.FullName), header.WrapperClassName))
            .OrderBy(static header => header.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var includeDirectories = _options.IncludeDirectories
            .Select(static directory => Path.GetFullPath(directory.FullName))
            .ToArray();
        string[] arguments =
        [
            "-x", "c++", "--target=x86_64-pc-windows-msvc", "-std=c++" + options.CppVersion,
            .. includeDirectories.Select(static directory => "-I" + directory),
            .. _options.CompileDefinitions.Select(static definition => "-D" + definition),
        ];

        var functions = new Dictionary<string, BindingFreeFunctionDefinition>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wrapper = header.WrapperClassName ?? DefaultWrapperName(header.Path);
            ValidateWrapperName(wrapper, header.Path);
            foreach (var function in CAbiCompilerDiscovery.Discover(header.Path, wrapper, arguments, cancellationToken))
            {
                if (!functions.TryAdd(function.NativeExport, function))
                {
                    throw new InvalidOperationException(
                        $"C symbol '{function.NativeExport}' is declared in more than one selected header.");
                }
            }
        }

        var exportPrefix = ChooseExportPrefix(functions);
        var errorClear = exportPrefix + "ClearError";
        var profile = new BindingEmissionProfile()
        {
            CSharpNamespace = options.CSharpNamespace,
            IsInternal = options.IsInternal,
            ManagedNativeErrorProjection = "global::TedToolkit.CppBindings.NativeErrorProjection",
            NativeErrorClearExport = errorClear,
            NativeErrorSetter = exportPrefix + "SetError",
            NativePreamble = "",
            NativeErrorPreamble = "",
        };
        var operations = functions.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value with { NativeExport = exportPrefix + "Call_" + pair.Key, })
            .ToArray();
        var api = new BindingFiniteProfileApi(
            "CAbiBindings.g.cs",
            "CAbiBindings.cpp",
            [], [], [], [], [],
            operations,
            "#include <cstdlib>\n#include <cstring>\n#include <memory>\n#include <stdexcept>\n#include <typeinfo>\n"
            + string.Join("\n",
                headers.Select(static header => "#include " + QuotePath(header.Path))),
            "An external C linkage function threw an unknown native exception.",
            operations.Select(static operation => operation.NativeExport).Prepend(errorClear).ToArray(),
            []);
        var nativeProject = BindingCMakeProjectEmitter.CreateNativeProject(new(
            "CAbiBindings",
            options.NativeLibraryBaseName,
            options.CppVersion,
            [],
            compileDefinitions: _options.CompileDefinitions.Select(QuoteCMakeArgument),
            compileOptions: ["/EHs", "/EHc-",],
            includeDirectories: includeDirectories.Select(QuotePath),
            linkLibraries: _options.LinkLibraries.Select(QuotePath)));
        return Task.FromResult(new BindingProviderModel(
            [], [], profile, [], [], [], "unused", "unused", nativeProject, operations.Length == 0 ? [] : [api,]));
    }

    private static string DefaultWrapperName(string header)
    {
        var stem = Path.GetFileNameWithoutExtension(header);
        var name = string.Concat(stem.Select(static character =>
            SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_')) + "Wrapper";
        return SyntaxFacts.IsIdentifierStartCharacter(name[0]) ? name : "_" + name;
    }

    private static void ValidateWrapperName(string wrapper, string header)
    {
        if (CAbiCompilerDiscovery.IsManagedIdentifier(wrapper) && wrapper != "NativeApi")
        {
            return;
        }

        throw new ArgumentException($"'{wrapper}' is not an available wrapper class name for '{header}'.");
    }

    private static string ChooseExportPrefix(Dictionary<string, BindingFreeFunctionDefinition> functions)
    {
        var prefix = "TedToolkit_FreeFunction_";
        while (functions.Keys.Any(symbol => symbol.StartsWith(prefix, StringComparison.Ordinal)))
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

    private sealed class NoRecordPolicy : IBindingLayoutPolicy, IBindingTemplatePolicy
    {
        public BindingAdmission Admit(BindingLayoutDescriptor layout)
        {
            return BindingAdmission.Reject("Record layouts are outside the external C linkage profile.");
        }

        public BindingAdmission Admit(BindingTemplateDescriptor template)
        {
            return BindingAdmission.Reject("Templates are outside the external C linkage profile.");
        }
    }
}