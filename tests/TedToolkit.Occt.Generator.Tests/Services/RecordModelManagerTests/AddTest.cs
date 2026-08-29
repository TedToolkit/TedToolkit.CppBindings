// -----------------------------------------------------------------------
// <copyright file="AddTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using ClangSharp;
using ClangSharp.Interop;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Models.Declarations;
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
    private static readonly FieldInfo SourceBuilderField = typeof(SourceBuilder)
        .GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SourceBuilder internal buffer field was not found.");

    /// <summary>
    /// Verifies a used standard pair specialization receives a closed, compiler-probed model.
    /// </summary>
    [Test]
    public async Task Should_include_used_standard_pair_specialization_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace std
            {
                template<typename TFirst, typename TSecond>
                struct pair
                {
                    TFirst first;
                    TSecond second;
                };
            }
            template struct std::pair<double, double>;

            struct Range
            {
                std::pair<double, double> Bounds() const;
            };
            """, "__occt__/test.cpp");
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Range");
        var manager = CreateManager();

        manager.Add(record);

        var pair = manager.RecordModels.Single(static model =>
            model.Type.CppTypeName == "std::pair<double, double>");
        await Assert.That(pair.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["first", "second",]);
    }

    /// <summary>
    /// Verifies public enum-valued nested template specializations remain available.
    /// </summary>
    [Test]
    public async Task Should_include_public_nested_template_with_qualified_enum_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct NodeId
            {
                enum class Kind { Face };
                template<Kind Value> struct Typed { int Id; };
                Typed<Kind::Face> Face() const;
            };
            template struct NodeId::Typed<NodeId::Kind::Face>;
            """);
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "NodeId");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .Contains("NodeId::Typed<NodeId::Kind::Face>");
    }

    /// <summary>
    /// Verifies a specialization carrying a private forward-declared nested type is not public.
    /// </summary>
    [Test]
    public async Task Should_exclude_template_with_private_forward_declared_argument_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<typename T> struct Allocator { T* Value; };
            class Cache
            {
            private:
                struct Slot;
                Allocator<Slot> Storage;
            };
            """);
        var cache = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Cache");
        var specialization = cache.Fields.Single().Type.CanonicalType.AsCXXRecordDecl
            ?? throw new InvalidOperationException("Allocator specialization was not found.");
        var manager = CreateManager();

        manager.Add(specialization);

        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .DoesNotContain("Allocator<Cache::Slot>");
    }

    /// <summary>
    /// Verifies C++ standard stream ownership APIs are excluded while unrelated methods remain.
    /// </summary>
    [Test]
    public async Task Should_include_representable_stream_and_handle_dependencies_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace std
            {
                template<typename T> struct basic_istream {};
                template<typename T> struct basic_ostream {};
                template<typename T> struct basic_streambuf {};
                template<typename T> struct shared_ptr { T* Value; };
            }

            template struct std::basic_istream<char>;
            template struct std::basic_ostream<char>;
            template struct std::basic_streambuf<char>;
            template struct std::shared_ptr<std::basic_istream<char>>;
            template struct std::shared_ptr<std::basic_ostream<char>>;
            template struct std::shared_ptr<std::basic_streambuf<char>>;

            template<typename T> struct NCollection_Handle { void* Value; };

            struct FileSystem
            {
                NCollection_Handle<int> InternalHandle;
                int IsSupportedPath() const;
                std::shared_ptr<std::basic_istream<char>> OpenIStream();
                std::shared_ptr<std::basic_ostream<char>> OpenOStream();
                void AcceptBuffer(const std::shared_ptr<std::basic_streambuf<char>>& value);
                NCollection_Handle<int> UnsupportedHandle();
                void AcceptHandle(const NCollection_Handle<int>& value);
            };
            """);
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "FileSystem");
        var manager = CreateManager();

        manager.Add(record);

        var fileSystem = manager.RecordModels.Single(static model => model.Type.CppTypeName == "FileSystem");

        await Assert.That(fileSystem.MethodModels.Select(static method => method.MethodName))
            .IsEquivalentTo([
                "IsSupportedPath",
                "OpenIStream",
                "OpenOStream",
                "AcceptBuffer",
                "UnsupportedHandle",
                "AcceptHandle",
            ]);
        await Assert.That(fileSystem.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["InternalHandle",]);
        await Assert.That(manager.RecordModels.Select(static model => model.Type.CppTypeName))
            .Contains("std::shared_ptr<std::basic_istream<char>>");
    }

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
        await Assert.That(Render(manager.EnumModels.Single().Members
                .Single(static member => member.Name == "Quantity_TypeOfColor_RGB")
                .DescriptionItems.Single()))
            .Contains("RGB space.");
    }

    /// <summary>
    /// Verifies constructors, destructors, and operators are classified correctly.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_only_declared_callable_methods_Async()
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
            (MethodModelType.NEW, "New"),
            (MethodModelType.DELETE, "Delete"),
            (MethodModelType.NORMAL, "OwnMethod"),
            (MethodModelType.OPERATOR, "=="),
        ]);
    }

    /// <summary>
    /// Verifies non-callable methods are excluded from generated method models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_non_callable_methods_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                void Reset() = delete;
                void Legacy() __attribute__((unavailable("legacy API")));
                void Write(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName, ParameterCount: m.Parameters.Count))
            .ToArray();

        await Assert.That(methods.Length).IsEqualTo(2);
        await Assert.That(methods).Contains((MethodModelType.NEW, "New", 0));
        await Assert.That(methods).Contains((MethodModelType.NORMAL, "Write", 1));
    }

    /// <summary>
    /// Verifies deleted copy constructors are excluded from generated constructor models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_deleted_copy_constructor_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                StreamLike(const StreamLike&) = delete;
                void Write(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var constructors = manager.RecordModels.Single().MethodModels
            .Where(static m => m.Type == MethodModelType.NEW)
            .Select(static m => m.Parameters.Count)
            .ToArray();

        await Assert.That(constructors).IsEquivalentTo([0,]);
    }

    /// <summary>
    /// Verifies constructors that take non-copyable record values are excluded.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_constructors_with_non_copyable_value_parameters_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                StreamLike(const StreamLike&) = delete;
            };

            struct Holder
            {
                Holder(StreamLike stream);
                Holder(int value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");

        var manager = CreateManager();

        manager.Add(record);

        var constructors = manager.RecordModels.Single().MethodModels
            .Where(static m => m.Type == MethodModelType.NEW)
            .Select(static m => m.Parameters.Select(p => p.Type.CppTypeName).ToArray())
            .ToArray();

        await Assert.That(constructors.Length).IsEqualTo(1);
        await Assert.That(constructors.Single()).IsEquivalentTo(["int",]);
    }

    /// <summary>
    /// Verifies methods with unnamed parameters are excluded from generated method models.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_methods_with_unnamed_parameters_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct StreamLike
            {
                StreamLike();
                void Write(int);
                void Flush();
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "StreamLike");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Select(static m => (m.Type, m.MethodName, ParameterCount: m.Parameters.Count))
            .ToArray();

        await Assert.That(methods).IsEquivalentTo([
            (MethodModelType.NEW, "New", 0),
            (MethodModelType.NORMAL, "Flush", 0),
        ]);
    }

    /// <summary>
    /// Verifies fields whose types are anonymous declarations are excluded from the model.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_fields_with_anonymous_types_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Holder
            {
                enum
                {
                    Value,
                } InternalState;
                union
                {
                    double Floating;
                    long long Integer;
                } InternalStorage;
                int PublicValue;
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Holder");

        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Single().FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["PublicValue",]);
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
            (MethodModelType.IMPLICIT, "Implicit"),
            (MethodModelType.EXPLICIT, "Explicit"),
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
    /// Verifies a derived representation does not redeclare fields owned by its base representation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_model_only_fields_declared_by_each_record_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Base
            {
                int SharedName;
            };

            struct Derived : Base
            {
                int SharedName;
            };
            """, "__occt__/test.cpp");
        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static value => value.Name == "Derived");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Single(static model => model.Type.CppTypeName == "Base")
            .FieldModels.Select(static field => field.Name)).IsEquivalentTo(["SharedName",]);
        await Assert.That(manager.RecordModels.Single(static model => model.Type.CppTypeName == "Derived")
            .FieldModels.Select(static field => field.Name)).IsEquivalentTo(["SharedName",]);
    }

    /// <summary>
    /// Verifies handle specializations are unwrapped to the referenced record before model creation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the handle field does not resolve to a C++ record.</exception>
    [Test]
    public async Task Should_unwrap_handle_specializations_before_adding_record_models_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace opencascade
            {
                template<typename T>
                class handle
                {
                public:
                    handle();
                    explicit handle(const T* value);
                    T* get() const;
                };
            }

            struct Geom2d_Curve
            {
                int Value;
            };

            struct Holder
            {
                opencascade::handle<Geom2d_Curve> Value;
            };
            """, "__occt__/test.cpp");

        var handleRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder")
            .Fields
            .Single()
            .Type
            .AsCXXRecordDecl
            ?? throw new InvalidOperationException("Handle field type did not resolve to a CXX record declaration.");

        var manager = CreateManager();

        var model = manager.Add(handleRecord);

        await Assert.That(model.Type.CppTypeName).IsEqualTo("Geom2d_Curve");
        await Assert.That(model.FieldModels.Single().Name).IsEqualTo("Value");
        await Assert.That(manager.RecordModels.Select(static m => m.Type.CppTypeName))
            .IsEquivalentTo(["Geom2d_Curve",]);
    }

    /// <summary>
    /// Verifies fields whose handle target is only forward declared are excluded from record generation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_handle_fields_with_incomplete_targets_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            namespace opencascade
            {
                template<typename T>
                class handle
                {
                public:
                    handle();
                };
            }

            struct Holder
            {
                struct Base;
                opencascade::handle<Base> Internal;
                int Value;
            };
            """, "__occt__/test.cpp");

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Holder");
        var manager = CreateManager();

        var model = manager.Add(record);

        await Assert.That(model.FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["Value",]);
        await Assert.That(manager.RecordModels.Select(static item => item.Type.CppTypeName))
            .IsEquivalentTo(["Holder",]);
    }

    /// <summary>
    /// Verifies specializations that expose a private template argument are excluded as unnameable dependencies.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_exclude_specializations_with_private_template_arguments_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template<typename T>
            struct Wrapper
            {
                T Value;
            };

            struct Owner
            {
            private:
                enum class State
                {
                    Ready,
                };

            public:
                Wrapper<State> Internal;
                int Value;
            };
            """, "__occt__/test.cpp");

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Owner");
        var manager = CreateManager();

        manager.Add(record);

        await Assert.That(manager.RecordModels.Select(static item => item.Type.CppTypeName))
            .IsEquivalentTo(["Owner",]);
        await Assert.That(manager.RecordModels.Single().FieldModels.Select(static field => field.Name))
            .IsEquivalentTo(["Value",]);
    }

    /// <summary>
    /// Verifies template specialization field types collect both the template and template-argument headers.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collect_template_argument_headers_for_field_types_Async()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var pointHeaderPath = Path.Combine(folder.FullName, "gp_Pnt2d.hxx");
            var arrayHeaderPath = Path.Combine(folder.FullName, "NCollection_Array1.hxx");
            var sourcePath = Path.Combine(folder.FullName, "test.cpp");

            await File.WriteAllTextAsync(pointHeaderPath, """
                struct gp_Pnt2d
                {
                };
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(arrayHeaderPath, """
                template <typename T>
                struct NCollection_Array1
                {
                    T Value() const;
                };
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(sourcePath, """
                #include "gp_Pnt2d.hxx"
                #include "NCollection_Array1.hxx"

                struct Holder
                {
                    NCollection_Array1<gp_Pnt2d> Field;
                };
                """).ConfigureAwait(false);

            using var translationUnit =
                ParseTranslationUnit(await File.ReadAllTextAsync(sourcePath).ConfigureAwait(false), sourcePath);
            var record = translationUnit.TranslationUnitDecl.CursorChildren
                .OfType<CXXRecordDecl>()
                .Single(static r => r.Name == "Holder");

            var manager = CreateManager();

            manager.Add(record);

            var requiredHeaders = manager.RecordModels.Single(static m => m.Type.CppTypeName == "Holder")
                .FieldModels.Single()
                .Type.RequiredHeaders;
            await Assert.That(requiredHeaders).IsEquivalentTo(["NCollection_Array1.hxx", "gp_Pnt2d.hxx",]);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies placeholder template specializations are omitted while fixed specializations remain generatable.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_only_fixed_template_specializations_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            template <typename T = void>
            struct Traverse
            {
            };

            struct Owner
            {
                Traverse<> Placeholder;
                Traverse<int> Fixed;
            };
            """);
        var owner = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static record => record.Name == "Owner");
        var specializations = owner.Fields
            .Select(static field => field.Type.AsCXXRecordDecl?.Definition)
            .OfType<CXXRecordDecl>()
            .ToArray();
        var manager = CreateManager();

        foreach (var specialization in specializations)
        {
            manager.Add(specialization);
        }

        await Assert.That(manager.RecordModels.Select(static record => record.Type.CppTypeName))
            .IsEquivalentTo(["Traverse<int>",]);
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

    /// <summary>
    /// 验证左值引用、右值引用和指针参数在 PInvoke 签名一致时会折叠为一个方法。
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_collapse_pointer_and_reference_overloads_when_pinvoke_signature_matches_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Geom_Surface
            {
            };

            struct Value
            {
                void Attach(Geom_Surface* value);
                void Attach(Geom_Surface& value);
                void Attach(Geom_Surface&& value);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single(static m => m.Type.CppTypeName == "Value").MethodModels
            .Where(static m => m.MethodName == "Attach")
            .ToArray();

        await Assert.That(methods).HasSingleItem();
        await Assert.That(methods.Single().Parameters.Single().Type.CSharpPInvokeType.ToCode())
            .IsEqualTo("Geom_Surface*");
    }

    /// <summary>
    /// 验证赋值类操作符会标记为返回自身以匹配 pinvoke 包装。
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_mark_assignment_operator_as_return_self_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Value
            {
                Value& operator+=(const Value& other);
                Value& operator-=(const Value& other);
            };
            """);

        var record = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "Value");

        var manager = CreateManager();

        manager.Add(record);

        var methods = manager.RecordModels.Single().MethodModels
            .Where(static m => m.Type == MethodModelType.OPERATOR)
            .OrderBy(static m => m.MethodName)
            .ToArray();

        await Assert.That(methods.Select(static m => m.MethodName))
            .IsEquivalentTo(["+=", "-=",]);
        await Assert.That(methods.All(static m => m.ReturnType.CppTypeName == "Value &")).IsTrue();
        await Assert.That(methods.All(static m => m.ReturnSelf)).IsTrue();
    }

    /// <summary>
    /// Verifies Standard_Transient ancestry is projected independently from other inheritance.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_project_standard_transient_ancestry_only_for_matching_records_Async()
    {
        using var translationUnit = ParseTranslationUnit("""
            struct Standard_Transient
            {
            };

            struct HeapOnlyBase
            {
            };

            struct HeapOnlyDerived : HeapOnlyBase
            {
                HeapOnlyDerived();
            };

            struct TransientDerived : Standard_Transient
            {
                TransientDerived();
            };

            template<typename T>
            struct TransientTemplate
            {
            private:
                struct Base : Standard_Transient
                {
                };

            public:
                struct Static : Base
                {
                };
            };

            struct NestedTransientDerived : TransientTemplate<int>::Static
            {
            };
            """, "__occt__/test.cpp");

        var heapOnlyRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "HeapOnlyDerived");
        var transientRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "TransientDerived");
        var nestedTransientRecord = translationUnit.TranslationUnitDecl.CursorChildren
            .OfType<CXXRecordDecl>()
            .Single(static r => r.Name == "NestedTransientDerived");

        var manager = CreateManager();

        var heapOnlyModel = manager.Add(heapOnlyRecord);
        var transientModel = manager.Add(transientRecord);
        var nestedTransientModel = manager.Add(nestedTransientRecord);

        await Assert.That(heapOnlyModel.IsStandardTransient).IsFalse();
        await Assert.That(transientModel.IsStandardTransient).IsTrue();
        await Assert.That(nestedTransientModel.IsStandardTransient).IsTrue();
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

    private static TranslationUnit ParseTranslationUnit(string source, string filePath = "test.cpp")
    {
        using var file = CXUnsavedFile.Create(filePath, source);
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            filePath,
            ["-std=c++20", "-x", "c++",],
            [file,],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        return TranslationUnit.GetOrCreate(translationUnit);
    }

    private static string Render(IToDescription description)
    {
        var builder = new SourceBuilder();
        description.ToDescription(ref builder);
        return SourceBuilderField.GetValue(builder)?.ToString()
               ?? throw new InvalidOperationException("Unable to render RoslynHelper description.");
    }

    private sealed class FakeVcpkgEnvironment : IVcpkgEnvironment
    {
        public Task<string> GetIncludingHeaderContentAsync(
            string triplet,
            IReadOnlyList<DeclOptions> declarations,
            CancellationToken cancellationToken)
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