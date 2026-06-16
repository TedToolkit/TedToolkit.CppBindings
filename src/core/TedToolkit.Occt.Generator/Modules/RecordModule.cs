// -----------------------------------------------------------------------
// <copyright file="RecordModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Generated;
using ModularPipelines.Modules;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Collects the record declarations that need to be generated.
/// </summary>
/// <param name="generationOptions">The generation options.</param>
/// <param name="recordManager">The record queue manager.</param>
/// <param name="recordService">The record metadata service.</param>
/// <param name="typeService">The type naming service.</param>
[DependsOn<ParseModule>]
public sealed class RecordModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordManager recordManager,
    IRecordService recordService,
    ITypeService typeService) :
    Module<TranslationUnit>
{
    /// <inheritdoc />
    protected override async Task<TranslationUnit?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var parseModule = await context.GetParseModule();

        var translationUnit = parseModule.ValueOrDefault
                                    ?? throw new InvalidOperationException("TranslationUnit is null");

        var names = generationOptions.Value.DeclOptions.Select(i => i.FileName).ToArray();

        foreach (var cxxRecordDecl in translationUnit.TranslationUnitDecl.CursorChildren
                     .OfType<CXXRecordDecl>()
                     .Where(r => names.Contains(r.Name)))
        {
            AddRecordDecl(cxxRecordDecl);
        }

        return translationUnit;
    }

    private void AddRecordDecl(CXXRecordDecl decl)
    {
        decl = decl.Definition ?? decl;

        if (!recordManager.Add(decl))
        {
            return;
        }

        foreach (var fieldDecl in recordService.GetFields(decl))
        {
            AddType(fieldDecl.Type);
        }

        foreach (var methodDecl in recordService.GetMethods(decl))
        {
            AddType(methodDecl.ReturnType);
            foreach (var methodDeclParameter in methodDecl.Parameters)
            {
                AddType(methodDeclParameter.Type);
            }
        }
    }

    private void AddType(ClangSharp.Type type)
    {
        type = typeService.DesugarType(type);
        if (type.AsCXXRecordDecl is not { } decl)
        {
            return;
        }

        AddRecordDecl(decl);
    }
}