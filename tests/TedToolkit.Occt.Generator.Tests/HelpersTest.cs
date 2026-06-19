// -----------------------------------------------------------------------
// <copyright file="HelpersTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests;

internal sealed class HelpersTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    [Test]
    public async Task Should_map_builtin_types_to_expected_data_types_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Builtins
            {
                unsigned long UnsignedLongValue;
                long LongValue;
                wchar_t WideCharValue;
                char16_t Char16Value;
                char32_t Char32Value;
                unsigned long long UnsignedLongLongValue;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Builtins");

        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "UnsignedLongValue").Type.ToPInvokeDataType(), DataType.FromType<CULong>());
        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "LongValue").Type.ToPInvokeDataType(), DataType.FromType<CLong>());
        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "WideCharValue").Type.ToPInvokeDataType(), DataType.Char);
        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "Char16Value").Type.ToPInvokeDataType(), DataType.Char);
        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "Char32Value").Type.ToPInvokeDataType(), DataType.Uint);
        await AssertRenderedAsync(record.Fields.Single(static f => f.Name == "UnsignedLongLongValue").Type.ToPInvokeDataType(), DataType.Ulong);
    }

    [Test]
    public async Task Should_turn_pointer_and_references_into_pointer_layers_recursively_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Geom_Surface
            {
            };

            void Accept(
                int* intPointerValue,
                void* voidPointerValue,
                Geom_Surface& lValueReference,
                Geom_Surface&& rValueReference,
                Geom_Surface** doublePointerValue);
            """);

        var method = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static f => f.Name == "Accept");

        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "intPointerValue").Type.ToPInvokeDataType(), DataType.Int.Pointer);
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "voidPointerValue").Type.ToPInvokeDataType(), DataType.Void.Pointer);

        var geomSurface = new DataType("Geom_Surface");
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "lValueReference").Type.ToPInvokeDataType(), geomSurface.Pointer);
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "rValueReference").Type.ToPInvokeDataType(), geomSurface.Pointer);
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "doublePointerValue").Type.ToPInvokeDataType(), geomSurface.Pointer.Pointer);
    }

    [Test]
    public async Task Should_strip_const_qualifiers_from_pinvoke_type_names_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Geom_Surface
            {
            };

            void Accept(
                const int constIntValue,
                const Geom_Surface& constReferenceValue,
                const Geom_Surface* constPointerValue);
            """);

        var method = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static f => f.Name == "Accept");

        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "constIntValue").Type.ToPInvokeDataType(), DataType.Int);

        var geomSurface = new DataType("Geom_Surface");
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "constReferenceValue").Type.ToPInvokeDataType(), geomSurface.Pointer);
        await AssertRenderedAsync(method.Parameters.Single(static p => p.Name == "constPointerValue").Type.ToPInvokeDataType(), geomSurface.Pointer);
    }

    private static async Task AssertRenderedAsync(DataType actual, DataType expected)
    {
        await Assert.That(Render(actual)).IsEqualTo(Render(expected));
    }

    private static string Render(DataType dataType)
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
