// -----------------------------------------------------------------------
// <copyright file="OccHandleTypeRuleTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.Services.TypeResolverTests;

/// <summary>
/// Tests for <see cref="OccHandleTypeRule"/>.
/// </summary>
internal sealed class OccHandleTypeRuleTest
{
    /// <summary>
    /// Verifies <c>occ::handle&lt;T&gt;</c> is projected to a pointer-like interop model and references the inner record.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_project_occ_handle_to_pointer_and_inner_record_Async()
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

        IResolver resolver = new Resolver([new OccHandleTypeRule(),]);

        var model = resolver.Resolve(fieldType);

        await Assert.That(model.CppOriginalDisplayName).IsEqualTo("occ::handle<Geom_Surface>");
        await Assert.That(model.CppInteropType.ToString()).IsEqualTo("Geom_Surface *");
        await Assert.That(model.ReferencedRecord).IsNotNull();
        await Assert.That(model.ReferencedRecord!.Name).IsEqualTo("Geom_Surface");
    }

    /// <summary>
    /// Captures the concrete clang type nodes used for <c>occ::handle&lt;T&gt;</c>.
    /// </summary>
    /// <returns>A task that completes when the assertions have finished.</returns>
    [Test]
    public async Task Should_characterize_occ_handle_type_shape_Async()
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

        await Assert.That(fieldType.TypeClassSpelling).IsEqualTo("Elaborated");
        await Assert.That(fieldType.GetType().Name).IsEqualTo(nameof(ElaboratedType));
        await Assert.That(((ElaboratedType)fieldType).NamedType.GetType().Name).IsEqualTo(nameof(TemplateSpecializationType));
        await Assert.That(fieldType.CanonicalType.GetType().Name).IsEqualTo(nameof(RecordType));
        await Assert.That(((ElaboratedType)fieldType).Desugar.GetType().Name).IsEqualTo(nameof(TemplateSpecializationType));
        await Assert.That(((ElaboratedType)fieldType).Desugar.CanonicalType.GetType().Name).IsEqualTo(nameof(RecordType));
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
