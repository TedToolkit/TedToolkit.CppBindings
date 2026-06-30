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
using TedToolkit.Occt.Generator.Services.Interfaces;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Services.RecordModelManagerTests;

/// <summary>
/// Verifies <see cref="RecordModelManager.Add(ClangSharp.CXXRecordDecl)"/>.
/// </summary>
internal sealed class AddTest
{
    /// <summary>
    /// Verifies enums referenced by fields are collected and documented.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var manager = CreateManager();

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

    /// <summary>
    /// Verifies constructors, destructors, and operators are classified correctly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var manager = CreateManager();

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

    /// <summary>
    /// Verifies implicit and explicit conversion operators are classified correctly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName))
            .ToArray();

        await Assert.That(methods).IsEquivalentTo([
            (MethodModelType.Implicit, "operator bool"),
            (MethodModelType.Explicit, "operator int"),
        ]);
    }

    /// <summary>
    /// Verifies records referenced by fields are collected transitively.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
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

        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Count).IsEqualTo(2);
        await Assert.That(manager.RecordModels.Select(static m => m.Type.CppTypeName))
            .IsEquivalentTo(["Child", "Parent",]);
        await Assert.That(manager.RecordModels.Single(static m => m.Type.CppTypeName == "Parent")
            .FieldModels.Single().Type.CppTypeName)
            .IsEqualTo("Child");
    }

    /// <summary>
    /// Verifies non-const methods win when const and non-const overloads collapse to the same signature.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_prefer_non_const_method_when_csharp_signature_matches_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                int Coord();
                int Coord() const;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Where(static m => m.MethodName == "Coord")
            .ToArray();

        await Assert.That(methods).HasSingleItem();
        await Assert.That(methods.Single().IsConst).IsFalse();
    }

    private static RecordModelManager CreateManager()
    {
        return new(
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions()
            {
                DeclOptions = [],
                CSharpFolder = new(Path.GetTempPath()),
                CppFolder = new(Path.GetTempPath()),
            }),
            new Resolver([]),
            new FakeVcpkgDefaultTripletResolver(),
            new FakeVcpkgEnvironment());
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

    private sealed class FakeVcpkgEnvironment : IVcpkgEnvironment
    {
        public Task<string> GetIncludingHeaderContentAsync(string triplet, CancellationToken cancellationToken)
        {
            return Task.FromResult("");
        }

        public string GetRoot()
        {
            return "";
        }

        public string GetIncludeFolder(string triplet)
        {
            return "";
        }

        public string GetOcctIncludeFolder(string triplet)
        {
            return "__occt__";
        }
    }

    private sealed class FakeVcpkgDefaultTripletResolver : IVcpkgDefaultTripletResolver
    {
        public string GetTriplet()
        {
            return "x64-windows";
        }
    }
}