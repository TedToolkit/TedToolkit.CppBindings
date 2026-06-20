// -----------------------------------------------------------------------
// <copyright file="AddTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services;
using TedToolkit.Occt.Generator.Services.Rules;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Services.RecordModelManagerTests;

internal sealed class AddTest
{
    [Test]
    public async Task Should_collect_referenced_enums_when_record_fields_use_them_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            //! Supported color kinds.
            enum class Quantity_TypeOfColor : unsigned char
            {
                //! RGB space.
                Quantity_TypeOfColor_RGB = 1,
                Quantity_TypeOfColor_sRGB = 2,
            };

            //! Holder summary.
            struct Holder
            {
                //! Field summary.
                Quantity_TypeOfColor Field;

                //! Method summary.
                //! @param value Value summary.
                //! @return Return summary.
                int Method(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");

        var manager = new RecordModelManager(
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions
            {
                DeclOptions = [],
                CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                CppFolder = new DirectoryInfo(Path.GetTempPath()),
            }),
            new Resolver([new DefaultTypeRule(),]));

        manager.Add(record);

        await Assert.That(manager.RecordModels.Count).IsEqualTo(1);
        await Assert.That(manager.EnumModels.Count).IsEqualTo(1);
        await Assert.That(manager.RecordModels.Single().FieldModels.Single().Type.CppTypeName)
            .IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(manager.EnumModels.Single().Name).IsEqualTo("Quantity_TypeOfColor");
        await Assert.That(Render(manager.RecordModels.Single().DescriptionItems.Single()))
            .Contains("Holder summary.");
        await Assert.That(Render(manager.RecordModels.Single().FieldModels.Single().DescriptionItems.Single()))
            .Contains("Field summary.");
        await Assert.That(Render(manager.RecordModels.Single().MethodModels.Single().DescriptionItems.Single()))
            .Contains("Method summary.");
        await Assert.That(Render(manager.RecordModels.Single().MethodModels.Single().Parameters.Single().DescriptionItems.Single()))
            .Contains("Value summary.");
        await Assert.That(Render(new DescriptionReturns(manager.RecordModels.Single().MethodModels.Single().ReturnTypeDescriptionItems)))
            .Contains("Return summary.");
        await Assert.That(Render(manager.EnumModels.Single().DescriptionItems.Single()))
            .Contains("Supported color kinds.");
        await Assert.That(Render(manager.EnumModels.Single().Members.Single().DescriptionItems.Single()))
            .Contains("RGB space.");
    }

    [Test]
    public async Task Should_filter_special_methods_while_preserving_operators_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Base
            {
                Base();
                ~Base();
                void BaseMethod();
            };

            struct Derived : Base
            {
                Derived();
                ~Derived();
                void OwnMethod();
                bool operator==(const Derived&) const;
                static void* operator new(unsigned long long size);
                static void operator delete(void* ptr);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Derived");

        var manager = new RecordModelManager(
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions
            {
                DeclOptions = [],
                CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                CppFolder = new DirectoryInfo(Path.GetTempPath()),
            }),
            new Resolver([new DefaultTypeRule(),]));

        manager.Add(record);

        var methodNames = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName))
            .ToArray();

        await Assert.That(methodNames).IsEquivalentTo([
            (MethodModelType.Normal, "BaseMethod"),
            (MethodModelType.New, "New"),
            (MethodModelType.Delete, "Delete"),
            (MethodModelType.Normal, "OwnMethod"),
            (MethodModelType.Operator, "operator=="),
        ]);
    }

    [Test]
    public async Task Should_classify_conversion_operators_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                operator bool() const;
                explicit operator int() const;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = new RecordModelManager(
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions
            {
                DeclOptions = [],
                CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                CppFolder = new DirectoryInfo(Path.GetTempPath()),
            }),
            new Resolver([new DefaultTypeRule(),]));

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName))
            .ToArray();

        await Assert.That(methods).IsEquivalentTo([
            (MethodModelType.Implicit, "operator bool"),
            (MethodModelType.Explicit, "operator int"),
        ]);
    }

    [Test]
    public async Task Should_collect_referenced_records_when_record_fields_use_them_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Child
            {
                int Value;
            };

            struct Parent
            {
                Child Field;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Parent");

        var manager = new RecordModelManager(
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions
            {
                DeclOptions = [],
                CSharpFolder = new DirectoryInfo(Path.GetTempPath()),
                CppFolder = new DirectoryInfo(Path.GetTempPath()),
            }),
            new Resolver([new DefaultTypeRule(),]));

        manager.Add(record);

        await Assert.That(manager.RecordModels.Count).IsEqualTo(2);
        await Assert.That(manager.RecordModels.Select(static m => m.Type.CppTypeName))
            .IsEquivalentTo(["Child", "Parent",]);
        await Assert.That(manager.RecordModels.Single(static m => m.Type.CppTypeName == "Parent")
            .FieldModels.Single().Type.CppTypeName)
            .IsEqualTo("Child");
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

    private static string Render(IToDescription description)
    {
        var builder = new SourceBuilder();
        description.ToDescription(ref builder);
        return builder.ToString();
    }
}
