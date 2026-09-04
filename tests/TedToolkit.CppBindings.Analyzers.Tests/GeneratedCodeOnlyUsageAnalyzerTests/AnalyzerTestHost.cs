// -----------------------------------------------------------------------
// <copyright file="AnalyzerTestHost.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.CppBindings.Analyzers;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Analyzers.Tests.GeneratedCodeOnlyUsageAnalyzerTests;

/// <summary>
/// Compiles focused source fixtures and runs the Runtime analyzer against them.
/// </summary>
internal static class AnalyzerTestHost
{
    private static readonly ImmutableArray<MetadataReference> PlatformReferences = CreatePlatformReferences();

    private static readonly AnalyzerConfigOptionsProvider EmptyOptionsProvider = new GeneratedTreeOptionsProvider(null);

    /// <summary>
    /// Runs the analyzer against one source file.
    /// </summary>
    /// <param name="source">The source text to analyze.</param>
    /// <param name="path">The source path used for generated-code classification.</param>
    /// <returns>The Runtime analyzer diagnostics.</returns>
    /// <exception cref="InvalidOperationException">The fixture cannot be compiled.</exception>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string path = "Consumer.cs")
    {
        return AnalyzeAsync(CreateCompilation(source, path), EmptyOptionsProvider);
    }

    /// <summary>
    /// Runs the analyzer against source that intentionally exercises a compiler-rejected lifetime shape.
    /// </summary>
    /// <param name="source">The source text to analyze.</param>
    /// <param name="path">The source path used for generated-code classification.</param>
    /// <returns>The Runtime analyzer diagnostics.</returns>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAllowingCompilationErrorsAsync(
        string source,
        string path = "Consumer.cs")
    {
        return AnalyzeAsync(CreateCompilation(source, path), EmptyOptionsProvider, validateCompilation: false);
    }

    /// <summary>
    /// Runs the analyzer with the source tree configured as generated code.
    /// </summary>
    /// <param name="source">The source text to analyze.</param>
    /// <param name="path">The source path configured as generated.</param>
    /// <returns>The Runtime analyzer diagnostics.</returns>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeConfiguredGeneratedAsync(string source, string path)
    {
        return AnalyzeAsync(CreateCompilation(source, path), new GeneratedTreeOptionsProvider(path));
    }

    /// <summary>
    /// Runs the analyzer against source emitted by a Roslyn source generator.
    /// </summary>
    /// <param name="generatedSource">The source text emitted by the fixture generator.</param>
    /// <returns>The Runtime analyzer diagnostics.</returns>
    /// <exception cref="InvalidOperationException">The generated fixture cannot be compiled.</exception>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeGeneratorOutputAsync(string generatedSource)
    {
        var compilation = CreateCompilation("internal sealed class Anchor;", "Anchor.cs");
        var driver = CSharpGeneratorDriver
            .Create([new FixtureGenerator(generatedSource),])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updatedCompilation,
            out var generatorDiagnostics);

        var generatorErrors = generatorDiagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!generatorErrors.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, generatorErrors));
        }

        return AnalyzeAsync(updatedCompilation, EmptyOptionsProvider);
    }

    private static CSharpCompilation CreateCompilation(string source, string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview),
            path);
        var references = PlatformReferences
            .Add(MetadataReference.CreateFromFile(typeof(Owned<>).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(Handle<>).Assembly.Location));
        return CSharpCompilation.Create(
            "AnalyzerFixture",
            [syntaxTree,],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        bool validateCompilation = true)
    {
        if (validateCompilation)
        {
            var compilationErrors = compilation.GetDiagnostics()
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            if (!compilationErrors.IsEmpty)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, compilationErrors));
            }
        }

        var analyzerOptions = new AnalyzerOptions([], optionsProvider);
        return compilation
            .WithAnalyzers(
                [new GeneratedCodeOnlyUsageAnalyzer(), new ValueLifetimeAnalyzer(),],
                analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> CreatePlatformReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    private sealed class FixtureGenerator(string source) : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(output =>
                output.AddSource(
                    "GeneratedWrapper.cs",
                    SourceText.From("// <auto-generated/>\n" + source, Encoding.UTF8)));
        }
    }

    private sealed class GeneratedTreeOptionsProvider(string? generatedPath) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(
            ImmutableDictionary<string, string>.Empty);

        private static readonly AnalyzerConfigOptions GeneratedOptions = new DictionaryAnalyzerConfigOptions(
            ImmutableDictionary<string, string>.Empty.Add("generated_code", "true"));

        public override AnalyzerConfigOptions GlobalOptions
        {
            get
            {
                return EmptyOptions;
            }
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return string.Equals(tree.FilePath, generatedPath, StringComparison.OrdinalIgnoreCase)
                ? GeneratedOptions
                : EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> values)
        : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var configuredValue))
            {
                value = configuredValue;
                return true;
            }

            value = "";
            return false;
        }
    }
}