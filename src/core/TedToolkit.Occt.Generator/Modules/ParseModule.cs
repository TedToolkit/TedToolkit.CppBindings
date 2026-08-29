// -----------------------------------------------------------------------
// <copyright file="ParseModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Parses the OCCT headers into a translation unit.
/// </summary>
public sealed class ParseModule : Module<bool>
{
    private const string RELAY_FILE_NAME = "main.cpp";

    private readonly IRecordModelManager _recordModelManager;

    private readonly IOptions<GenerationOptions> _generationOptions;

    private readonly IVcpkgDefaultTripletResolver _defaultsResolver;

    private readonly IVcpkgEnvironment _vcpkgEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseModule"/> class.
    /// </summary>
    /// <param name="recordModelManager">The record model manager.</param>
    /// <param name="generationOptions">The generation options.</param>
    /// <param name="defaultsResolver">The default triplet resolver.</param>
    /// <param name="vcpkgEnvironment">The vcpkg environment service.</param>
    internal ParseModule(
        IRecordModelManager recordModelManager,
        IOptions<GenerationOptions> generationOptions,
        IVcpkgDefaultTripletResolver defaultsResolver,
        IVcpkgEnvironment vcpkgEnvironment)
    {
        _recordModelManager = recordModelManager;
        _generationOptions = generationOptions;
        _defaultsResolver = defaultsResolver;
        _vcpkgEnvironment = vcpkgEnvironment;
    }

    private Task<List<string>> CreateCommandLineArgsAsync()
    {
        var commandLineArgs = new List<string>(_generationOptions.Value.CommandLineArgs);
        var triplet = _generationOptions.Value.GetTriplet(_defaultsResolver);
        commandLineArgs.Add(ZString.Concat("-std=c++", _generationOptions.Value.CppVersion));
        commandLineArgs.Add("-x");
        commandLineArgs.Add("c++");
        commandLineArgs.Add(ZString.Concat("-I", _vcpkgEnvironment.GetOcctIncludeFolder(triplet)));
        commandLineArgs.Add(ZString.Concat("-I", _vcpkgEnvironment.GetIncludeFolder(triplet)));
        return Task.FromResult(commandLineArgs);
    }

    private static List<string> LogDiagnostics(IModuleContext context, ref CXTranslationUnit translationUnit)
    {
        var errors = new List<string>();
        for (uint i = 0; i < translationUnit.NumDiagnostics; i++)
        {
            using var cxDiagnostic = translationUnit.GetDiagnostic(i);
            var errorMessage = cxDiagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString();
            switch (cxDiagnostic.Severity)
            {
#pragma warning disable CA1848
                case CXDiagnosticSeverity.CXDiagnostic_Ignored:
                    if (context.Logger.IsEnabled(LogLevel.Debug))
                    {
                        context.Logger.LogDebug("{ClangDiagnostic}", errorMessage);
                    }

                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Note:
                    if (context.Logger.IsEnabled(LogLevel.Information))
                    {
                        context.Logger.LogInformation("{ClangDiagnostic}", errorMessage);
                    }

                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Warning:
                    if (context.Logger.IsEnabled(LogLevel.Warning))
                    {
                        context.Logger.LogWarning("{ClangDiagnostic}", errorMessage);
                    }

                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Error:
                    if (context.Logger.IsEnabled(LogLevel.Error))
                    {
                        context.Logger.LogError("{ClangDiagnostic}", errorMessage);
                    }

                    errors.Add(errorMessage);
                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Fatal:
                    if (context.Logger.IsEnabled(LogLevel.Critical))
                    {
                        context.Logger.LogCritical("{ClangDiagnostic}", errorMessage);
                    }

                    errors.Add(errorMessage);
                    break;
#pragma warning restore CA1848
            }
        }

        return errors;
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var triplet = _generationOptions.Value.GetTriplet(_defaultsResolver);

        var declarationsToInclude = _generationOptions.Value.DeclOptions;
        if (!_generationOptions.Value.GenerateAllPublicHeaders && declarationsToInclude.Count is 0)
        {
            throw new InvalidOperationException("At least one target declaration is required.");
        }

        using var file = CXUnsavedFile.Create(RELAY_FILE_NAME,
            _generationOptions.Value.GenerateAllPublicHeaders
                ? GetAllPublicHeaderContent(triplet)
                : await _vcpkgEnvironment
                    .GetIncludingHeaderContentAsync(triplet, declarationsToInclude, cancellationToken)
                    .ConfigureAwait(false));

        using var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RELAY_FILE_NAME,
            CollectionsMarshal.AsSpan(await CreateCommandLineArgsAsync().ConfigureAwait(false)),
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        using var unit = TranslationUnit.GetOrCreate(translationUnit);
        var errors = LogDiagnostics(context, ref translationUnit);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Clang parsing failed.{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        var declarations = unit.TranslationUnitDecl.CursorChildren.OfType<CXXRecordDecl>().ToArray();
        if (_generationOptions.Value.GenerateAllPublicHeaders)
        {
            var occtIncludeRoot = _vcpkgEnvironment.GetOcctIncludeFolder(triplet);
            var definitions = declarations
                .Select(static declaration => declaration.Definition)
                .OfType<CXXRecordDecl>()
                .Where(definition => IsDeclaredBelow(definition, occtIncludeRoot)
                                     && HasConcreteLayout(definition)
                                     && IsPubliclyNameable(definition))
                .DistinctBy(static definition => definition.CanonicalDecl.Handle);
            foreach (var definition in definitions)
            {
                _recordModelManager.Add(definition);
            }

            foreach (var enumDeclaration in unit.TranslationUnitDecl.CursorChildren
                         .OfType<EnumDecl>()
                         .Where(declaration => IsDeclaredBelow(declaration, occtIncludeRoot)
                                               && IsPubliclyNameable(declaration)))
            {
                _recordModelManager.Add(enumDeclaration);
            }

            return true;
        }

        var targets = _generationOptions.Value.DeclOptions
            .Select(static declaration => declaration.FileName)
            .Distinct(StringComparer.Ordinal);
        var resolvedDefinitions = new List<CXXRecordDecl>();
        foreach (var target in targets)
        {
            var definition = declarations
                .Where(declaration => string.Equals(declaration.Name, target, StringComparison.Ordinal))
                .Select(static declaration => declaration.Definition)
                .OfType<CXXRecordDecl>()
                .FirstOrDefault();
            if (definition is null)
            {
                throw new InvalidOperationException(
                    $"Requested declaration '{target}' was not defined by its selected public header.");
            }

            resolvedDefinitions.Add(definition);
        }

        foreach (var definition in resolvedDefinitions)
        {
            _recordModelManager.Add(definition);
        }

        return true;
    }

    private static bool IsDeclaredBelow(CXXRecordDecl declaration, string root)
    {
        return IsDeclaredBelow(declaration.Handle, root);
    }

    private static bool IsDeclaredBelow(EnumDecl declaration, string root)
    {
        return IsDeclaredBelow(declaration.Handle, root);
    }

    private static bool IsDeclaredBelow(CXCursor declaration, string root)
    {
        clang.getCursorLocation(declaration).GetFileLocation(out var file, out _, out _, out _);
        var path = file.Name.CString;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
               && path.Length > root.Length
               && (path[root.Length] is '\\' or '/')
               && path.EndsWith(".hxx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasConcreteLayout(CXXRecordDecl declaration)
    {
        var type = clang.getCanonicalType(declaration.TypeForDecl.Handle);
        return clang.Type_getSizeOf(type) >= 0;
    }

    private static bool IsPubliclyNameable(CXXRecordDecl declaration)
    {
        var parentKind = clang.getCursorSemanticParent(declaration.Handle).kind;
        return parentKind is CXCursorKind.CXCursor_TranslationUnit or CXCursorKind.CXCursor_Namespace
               && !string.IsNullOrWhiteSpace(declaration.Name)
               && !declaration.TypeForDecl.AsString.Contains("(unnamed", StringComparison.Ordinal);
    }

    private static bool IsPubliclyNameable(EnumDecl declaration)
    {
        var parentKind = clang.getCursorSemanticParent(declaration.Handle).kind;
        return parentKind is CXCursorKind.CXCursor_TranslationUnit or CXCursorKind.CXCursor_Namespace
               && !string.IsNullOrWhiteSpace(declaration.Name)
               && !declaration.TypeForDecl.AsString.Contains("(unnamed", StringComparison.Ordinal)
               && !declaration.TypeForDecl.AsString.Contains("(anonymous", StringComparison.Ordinal);
    }

    private string GetAllPublicHeaderContent(string triplet)
    {
        var headerPaths = Directory.EnumerateFiles(
                _vcpkgEnvironment.GetOcctIncludeFolder(triplet),
                "*.hxx")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var includeRoot = _vcpkgEnvironment.GetOcctIncludeFolder(triplet);
        var availableHeaders = Directory.EnumerateFiles(includeRoot)
            .Select(static path => Path.GetFileName(path)!)
            .ToHashSet(StringComparer.Ordinal);
        var dependencies = headerPaths.ToDictionary(
            static path => Path.GetFileName(path)!,
            GetOcctHeaderDependencies,
            StringComparer.Ordinal);
        var excludedHeaders = dependencies
            .Where(pair => pair.Value.Any(dependency => !availableHeaders.Contains(dependency)))
            .Select(static pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        _ = excludedHeaders.Add("MathLin_Jacobi.hxx");
        _ = excludedHeaders.Add("OpenGl_GLESExtensions.hxx");
        while (dependencies
               .Where(pair => !excludedHeaders.Contains(pair.Key)
                              && pair.Value.Any(excludedHeaders.Contains))
               .Select(static pair => pair.Key)
               .Any(excludedHeaders.Add))
        {
        }

        var reportPath = Path.Combine(
            _generationOptions.Value.CppFolder.Parent?.FullName
            ?? _generationOptions.Value.CppFolder.FullName,
            "unsupported-headers.txt");
        File.WriteAllLines(reportPath, excludedHeaders.Order(StringComparer.Ordinal));

        var builder = new System.Text.StringBuilder();
        foreach (var path in headerPaths.Where(path => !excludedHeaders.Contains(Path.GetFileName(path)!)))
        {
            _ = builder.Append("#include <")
                .Append(Path.GetFileName(path))
                .AppendLine(">");
        }

        return builder.ToString();
    }

    private static List<string> GetOcctHeaderDependencies(string path)
    {
        var dependencies = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#include", StringComparison.Ordinal))
            {
                continue;
            }

            var start = trimmed.IndexOfAny(['<', '"',]);
            if (start < 0)
            {
                continue;
            }

            var terminator = trimmed[start] is '<' ? '>' : '"';
            var end = trimmed.IndexOf(terminator, start + 1);
            if (end > start)
            {
                var dependency = trimmed[(start + 1)..end];
                if (IsOcctIncludeFile(dependency))
                {
                    dependencies.Add(dependency);
                }
            }
        }

        return dependencies;
    }

    private static bool IsOcctIncludeFile(string path)
    {
        return path.EndsWith(".hxx", StringComparison.Ordinal)
               || path.EndsWith(".pxx", StringComparison.Ordinal)
               || path.EndsWith(".lxx", StringComparison.Ordinal)
               || path.EndsWith(".gxx", StringComparison.Ordinal);
    }
}