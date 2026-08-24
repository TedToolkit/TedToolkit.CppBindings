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

        using var file = CXUnsavedFile.Create(RELAY_FILE_NAME,
            await _vcpkgEnvironment
                .GetIncludingHeaderContentAsync(triplet, _generationOptions.Value.DeclOptions, cancellationToken)
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
}