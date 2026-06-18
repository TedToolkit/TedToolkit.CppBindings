// -----------------------------------------------------------------------
// <copyright file="ResolveTypeModelTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using System.Reflection;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Tests.TypeServiceTests;

/// <summary>
/// Tests for integrating the type resolver into <see cref="TypeService"/>.
/// </summary>
internal sealed class ResolveTypeModelTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies <c>occ::handle&lt;T&gt;</c> resolves through the shared type service.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_resolve_occ_handle_through_type_service_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace occ
            {
                template <class T>
                class handle
                {
                };
            }

            class Geom_Surface
            {
            };

            struct Holder
            {
                occ::handle<Geom_Surface> Field;
            };
            """);

        var fieldType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder")
            .Fields
            .Single()
            .Type;

        var typeService = new TypeService(
            Microsoft.Extensions.Options.Options.Create(
                new GenerationOptions()
                {
                    DeclOptions = [],
                    CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                    CppFolder = new DirectoryInfo(Path.GetTempPath()),
                }),
            new Resolver([new OccHandleTypeRule(),]));

        var resolved = typeService.Resolve(fieldType);

        await Assert.That(typeService.GetReferencedRecord(fieldType)).IsNotNull();
        await Assert.That(typeService.GetReferencedRecord(fieldType)!.Name).IsEqualTo("Geom_Surface");
        await Assert.That(typeService.GetCSharpName(fieldType)).IsEqualTo("Geom_Surface");
        await Assert.That(Render(resolved.CSharpPInvokeType)).IsEqualTo("Geom_Surface *");
    }

    private static string Render(TedToolkit.RoslynHelper.Generators.Syntaxes.DataType dataType)
    {
        var sourceBuilder = new SourceBuilder();
        dataType.ToCode(ref sourceBuilder);
        return SourceBuilderField.GetValue(sourceBuilder)?.ToString()
               ?? throw new InvalidOperationException("Unable to render RoslynHelper DataType.");
    }

    private static TranslationUnit ParseTranslationUnit(string source)
    {
        using var file = CXUnsavedFile.Create("test.cpp", source);
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            "test.cpp",
            ["-std=c++20", "-x", "c++",],
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        return TranslationUnit.GetOrCreate(translationUnit);
    }
}
