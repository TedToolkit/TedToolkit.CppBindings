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

/// <summary>
/// Verifies helper type-projection utilities.
/// </summary>
internal sealed class HelpersTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies template pointer arguments remain distinct in generated type names.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_preserve_pointer_identity_in_generated_type_names_Async()
    {
        await Assert.That("NCollection_Allocator<NCollection_Mat4<float> *>".ToGeneratedTypeName())
            .IsEqualTo("NCollection_Allocator_NCollection_Mat4_float_Ptr");
        await Assert.That("NCollection_Allocator<NCollection_Mat4<float>>".ToGeneratedTypeName())
            .IsEqualTo("NCollection_Allocator_NCollection_Mat4_float");
    }

    /// <summary>
    /// Verifies long physical file names are shortened deterministically without changing ordinary names.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_shorten_only_long_generated_file_names_Async()
    {
        await Assert.That("gp_Pnt".ToGeneratedFileStem()).IsEqualTo("gp_Pnt");

        var longName = new string('A', 200);
        var shortened = longName.ToGeneratedFileStem();
        await Assert.That(shortened.Length).IsEqualTo(120);
        await Assert.That(shortened).IsEqualTo(longName.ToGeneratedFileStem());
        await Assert.That(shortened).IsNotEqualTo((longName + "B").ToGeneratedFileStem());
    }

    /// <summary>
    /// Verifies builtin native types map to the expected managed data types.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var unsignedLongValue = record.Fields.Single(static f => f.Name == "UnsignedLongValue").Type.ToPInvokeDataType();
        var longValue = record.Fields.Single(static f => f.Name == "LongValue").Type.ToPInvokeDataType();
        var wideCharValue = record.Fields.Single(static f => f.Name == "WideCharValue").Type.ToPInvokeDataType();
        var char16Value = record.Fields.Single(static f => f.Name == "Char16Value").Type.ToPInvokeDataType();
        var char32Value = record.Fields.Single(static f => f.Name == "Char32Value").Type.ToPInvokeDataType();
        var unsignedLongLongValue = record.Fields.Single(static f => f.Name == "UnsignedLongLongValue").Type.ToPInvokeDataType();

        await AssertRenderedAsync(unsignedLongValue, DataType.FromType<CULong>()).ConfigureAwait(false);
        await AssertRenderedAsync(longValue, DataType.FromType<CLong>()).ConfigureAwait(false);
        await AssertRenderedAsync(wideCharValue, DataType.Char).ConfigureAwait(false);
        await AssertRenderedAsync(char16Value, DataType.Char).ConfigureAwait(false);
        await AssertRenderedAsync(char32Value, DataType.Uint).ConfigureAwait(false);
        await AssertRenderedAsync(unsignedLongLongValue, DataType.Ulong).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies pointers and references add the expected pointer layers.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var intPointerValue = method.Parameters.Single(static p => p.Name == "intPointerValue").Type.ToPInvokeDataType();
        var voidPointerValue = method.Parameters.Single(static p => p.Name == "voidPointerValue").Type.ToPInvokeDataType();
        var publicVoidPointerValue = method.Parameters.Single(static p => p.Name == "voidPointerValue")
            .Type.ToPublicDataType();

        var lValueReference = method.Parameters.Single(static p => p.Name == "lValueReference").Type.ToPInvokeDataType();
        var rValueReference = method.Parameters.Single(static p => p.Name == "rValueReference").Type.ToPInvokeDataType();
        var doublePointerValue = method.Parameters.Single(static p => p.Name == "doublePointerValue").Type.ToPInvokeDataType();

        await AssertRenderedAsync(intPointerValue, DataType.Int.Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(voidPointerValue, DataType.Void.Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(publicVoidPointerValue, DataType.Void.Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(lValueReference, new DataType("Geom_Surface").Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(rValueReference, new DataType("Geom_Surface").Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(doublePointerValue, new DataType("Geom_Surface").Pointer.Pointer).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies const qualifiers do not survive in projected P/Invoke type names.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var constIntValue = method.Parameters.Single(static p => p.Name == "constIntValue").Type.ToPInvokeDataType();

        var constReferenceValue = method.Parameters.Single(static p => p.Name == "constReferenceValue").Type.ToPInvokeDataType();
        var constPointerValue = method.Parameters.Single(static p => p.Name == "constPointerValue").Type.ToPInvokeDataType();

        await AssertRenderedAsync(constIntValue, DataType.Int).ConfigureAwait(false);
        await AssertRenderedAsync(constReferenceValue, new DataType("Geom_Surface").Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(constPointerValue, new DataType("Geom_Surface").Pointer).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies std::basic_string&lt;char&gt; references normalize to the same projected P/Invoke type.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_normalize_basic_string_reference_kinds_to_same_pinvoke_type_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace std
            {
                template<typename T>
                struct basic_string
                {
                };
            }

            void Accept(
                const std::basic_string<char>& constReferenceValue,
                std::basic_string<char>&& rValueReferenceValue);
            """);

        var method = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static f => f.Name == "Accept");

        var constReferenceValue = method.Parameters.Single(static p => p.Name == "constReferenceValue").Type.ToPInvokeDataType();
        var rValueReferenceValue = method.Parameters.Single(static p => p.Name == "rValueReferenceValue").Type.ToPInvokeDataType();

        await AssertRenderedAsync(constReferenceValue, new DataType("std_basic_string_char").Pointer).ConfigureAwait(false);
        await AssertRenderedAsync(rValueReferenceValue, new DataType("std_basic_string_char").Pointer).ConfigureAwait(false);
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