// -----------------------------------------------------------------------
// <copyright file="Utf8StringTypeRuleTest.cs" company="TedToolkit">
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
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Services.TypeResolverTests;

/// <summary>
/// Verifies <see cref="Utf8StringTypeRule"/> projections.
/// </summary>
internal sealed class Utf8StringTypeRuleTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies a direct const char pointer becomes a UTF-8 span projection.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_project_const_char_pointer_to_utf8_span_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            void Accept(const char* value);
            """);

        var parameterType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static f => f.Name == "Accept")
            .Parameters
            .Single()
            .Type;

        var resolver = new Resolver(
        [
            new Utf8StringTypeRule(),
        ]);

        var resolved = resolver.Resolve(parameterType);

        await Assert.That(resolved.Type.CppTypeName).IsEqualTo("const char *");
        await Assert.That(Render(resolved.Type.CSharpPInvokeType)).IsEqualTo("byte*");
        await Assert.That(Render(resolved.Type.CSharpPublicType)).IsEqualTo("global::System.ReadOnlySpan<byte>");
    }

    /// <summary>
    /// Verifies a typedef alias resolving to const char pointer also becomes a UTF-8 span projection.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_project_standard_cstring_alias_to_utf8_span_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            typedef const char* Standard_CString;
            void Accept(Standard_CString value);
            """);

        var parameterType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static f => f.Name == "Accept")
            .Parameters
            .Single()
            .Type;

        var resolver = new Resolver(
        [
            new Utf8StringTypeRule(),
        ]);

        var resolved = resolver.Resolve(parameterType);

        await Assert.That(resolved.Type.CppTypeName).IsEqualTo("const char *");
        await Assert.That(Render(resolved.Type.CSharpPInvokeType)).IsEqualTo("byte*");
        await Assert.That(Render(resolved.Type.CSharpPublicType)).IsEqualTo("global::System.ReadOnlySpan<byte>");
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