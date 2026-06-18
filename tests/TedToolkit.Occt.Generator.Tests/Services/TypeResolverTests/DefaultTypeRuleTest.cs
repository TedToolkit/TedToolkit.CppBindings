// -----------------------------------------------------------------------
// <copyright file="DefaultTypeRuleTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Tests.Services.TypeResolverTests;

internal sealed class DefaultTypeRuleTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    [Test]
    public async Task Should_project_enum_to_underlying_pinvoke_type_and_public_enum_name_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            enum class Quantity_TypeOfColor : unsigned char
            {
                Quantity_TypeOfColor_RGB = 1,
                Quantity_TypeOfColor_sRGB = 2,
            };

            struct Holder
            {
                Quantity_TypeOfColor Field;
            };
            """);

        var fieldType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder")
            .Fields
            .Single()
            .Type;

        IResolver resolver = new Resolver([new DefaultTypeRule(),]);

        var resolved = resolver.Resolve(fieldType.CanonicalType);

        await Assert.That(Render(resolved.Type.CSharpPInvokeType)).IsEqualTo("byte");
        await Assert.That(Render(resolved.Type.CSharpPublicType)).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(resolved.Decl).IsNull();
        await Assert.That(resolved.Enum).IsNotNull();
        await Assert.That(resolved.Enum!.Name).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(Render(resolved.Enum.UnderlyingType)).IsEqualTo("byte");
        await Assert.That(resolved.Enum.Members.Select(static x => $"{x.Name}={x.Value}").ToArray())
            .IsEquivalentTo(
                [
                    "Quantity_TypeOfColor_RGB=1",
                    "Quantity_TypeOfColor_sRGB=2",
                ]);
    }

    [Test]
    public async Task Should_return_record_decl_for_record_type_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Geom_Surface
            {
            };
            """);

        var recordType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Geom_Surface")
            .TypeForDecl
            .CanonicalType;

        IResolver resolver = new Resolver([new DefaultTypeRule(),]);

        var resolved = resolver.Resolve(recordType);

        await Assert.That(resolved.Decl).IsNotNull();
        await Assert.That(resolved.Decl!.Name).IsEqualTo("Geom_Surface");
        await Assert.That(resolved.Enum).IsNull();
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
