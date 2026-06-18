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
/// <param name="generationOptions">The generation options.</param>
/// <param name="vcpkgService">The vcpkg environment service.</param>
public sealed class ParseModule(
    IRecordModelManager recordModelManager,
    IOptions<GenerationOptions> generationOptions,
    IVcpkgService vcpkgService) : Module<bool>
{
    private const string RELAY_FILE_NAME = "main.cpp";

    private CXUnsavedFile CreateFile()
    {
        var stringBuilder = ZString.CreateStringBuilder();

        foreach (var valueDeclOption in generationOptions.Value.DeclOptions)
        {
            stringBuilder.Append("#include <");
            stringBuilder.Append(valueDeclOption.FileName);
            stringBuilder.AppendLine(".hxx>");
        }

        return CXUnsavedFile.Create(RELAY_FILE_NAME, stringBuilder.ToString());
    }

    private async Task<List<string>> CreateCommandLineArgsAsync()
    {
        var commandLineArgs = new List<string>(generationOptions.Value.CommandLineArgs);
        commandLineArgs.Add(ZString.Concat("-std=c++", await vcpkgService.GetOcctCppVersionAsync().ConfigureAwait(false)));
        commandLineArgs.Add("-x");
        commandLineArgs.Add("c++");
        commandLineArgs.Add(ZString.Concat("-I", vcpkgService.GetOcctIncludeFolder()));
        commandLineArgs.Add(ZString.Concat("-I", vcpkgService.GetIncludeFolder()));
        return commandLineArgs;
    }

    private void LogDiagnostics(IModuleContext context, ref CXTranslationUnit translationUnit)
    {
        for (uint i = 0; i < translationUnit.NumDiagnostics; i++)
        {
            using var cxDiagnostic = translationUnit.GetDiagnostic(i);
            var errorMessage = cxDiagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString();
            switch (cxDiagnostic.Severity)
            {
#pragma warning disable CA1848, CA2254
                case CXDiagnosticSeverity.CXDiagnostic_Ignored:
                    context.Logger.LogDebug(errorMessage);
                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Note:
                    context.Logger.LogInformation(errorMessage);
                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Warning:
                    context.Logger.LogWarning(errorMessage);
                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Error:
                    context.Logger.LogError(errorMessage);
                    break;

                case CXDiagnosticSeverity.CXDiagnostic_Fatal:
                    context.Logger.LogCritical(errorMessage);
                    break;
#pragma warning restore CA1848, CA2254
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var file = CreateFile();

        using var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RELAY_FILE_NAME,
            CollectionsMarshal.AsSpan( await CreateCommandLineArgsAsync().ConfigureAwait(false)),
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        LogDiagnostics(context, ref translationUnit);

        using var unit = TranslationUnit.GetOrCreate(translationUnit);

        var names = generationOptions.Value.DeclOptions.Select(i => i.FileName).ToArray();
        foreach (var cxxRecordDecl in unit.TranslationUnitDecl.CursorChildren
                     .OfType<CXXRecordDecl>()
                     .Where(r => names.Contains(r.Name)))
        {
            recordModelManager.Add(cxxRecordDecl);
        }

        return true;
    }
}