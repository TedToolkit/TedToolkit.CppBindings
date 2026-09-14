// -----------------------------------------------------------------------
// <copyright file="HandleTypeRuleTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using ClangSharp;
using ClangSharp.Interop;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Services;
using TedToolkit.CppBindings.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Services.TypeResolverTests;

/// <summary>
/// Verifies OCCT smart-pointer layout projections.
/// </summary>
internal sealed class HandleTypeRuleTest
{
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies a const handle reference preserves borrowing through the generic lowercase layout.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_project_const_handle_reference_to_generic_handle_layout_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Standard_Transient { };
            namespace opencascade
            {
                template<typename T> struct handle { T* value; };
            }
            const opencascade::handle<Standard_Transient>& GetHandle();
            """);
        var returnType = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<FunctionDecl>()
            .Single(static function => function.Name == "GetHandle")
            .ReturnType;
        var resolver = new Resolver([new HandleTypeRule(),]);

        var resolved = resolver.Resolve(returnType);

        await Assert.That(Render(resolved.Type.CSharpPInvokeType))
            .IsEqualTo("global::TedToolkit.CppBindings.Occt.handle<Standard_Transient>");
        await Assert.That(Render(resolved.Type.CSharpPublicType))
            .IsEqualTo("global::TedToolkit.CppBindings.Occt.handle<Standard_Transient>");
        await Assert.That(resolved.Type.IsIntrusiveHandle).IsTrue();
        await Assert.That(resolved.Type.IntrusiveHandleElementType).IsEqualTo("Standard_Transient");
        await Assert.That(resolved.Type.IntrusiveHandleElementCppType).IsEqualTo("Standard_Transient");
        await Assert.That(resolved.Type.Transport.ValueIsConst).IsTrue();
        await Assert.That(resolved.Type.Transport.Indirections.Single().Kind)
            .IsEqualTo(TypeIndirectionKind.LValueReference);
        await Assert.That(resolved.Decl).IsNotNull();
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