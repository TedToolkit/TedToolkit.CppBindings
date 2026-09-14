// -----------------------------------------------------------------------
// <copyright file="DefaultTypeRuleTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.CppBindings.Occt.Generator.Services;
using TedToolkit.CppBindings.Occt.Generator.Services.Interfaces;
using TedToolkit.CppBindings.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Services.TypeResolverTests;

/// <summary>
/// Verifies the default branches of <see cref="Resolver"/>.
/// </summary>
internal sealed class DefaultTypeRuleTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies enum fields are projected to their underlying P/Invoke type and public enum type.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var resolver = new Resolver([]);

        var resolved = resolver.Resolve(fieldType);

        await Assert.That(Render(resolved.Type.CSharpPInvokeType)).IsEqualTo("byte");
        await Assert.That(Render(resolved.Type.CSharpPublicType)).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(resolved.Decl).IsNull();
        await Assert.That(resolved.Enum).IsNotNull();
        await Assert.That(resolved.Enum!.Name).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(resolved.Enum.IntegerType.AsString).IsEqualTo("unsigned char");
        await Assert.That(resolved.Enum.Enumerators.Select(static x => $"{x.Name}={x.InitVal}").ToArray())
            .IsEquivalentTo(
                [
                    "Quantity_TypeOfColor_RGB=1",
                    "Quantity_TypeOfColor_sRGB=2",
                ]);
    }

    /// <summary>
    /// Verifies record types surface the matching record declaration.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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
            .TypeForDecl;

        var resolver = new Resolver([]);

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