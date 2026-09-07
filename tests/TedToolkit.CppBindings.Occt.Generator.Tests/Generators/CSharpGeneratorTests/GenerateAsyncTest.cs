// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CSharpGeneratorTests;

/// <summary>
/// Verifies <see cref="TedToolkit.CppBindings.Occt.Generator.Generators.CSharpGenerator"/> output.
/// </summary>
internal sealed class GenerateAsyncTest
{
    /// <summary>
    /// Verifies omitted native base interfaces do not erase the proved intrusive handle constraint.
    /// </summary>
    /// <param name="publicBase">Whether the relation is public but the base is not emitted.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_handle_constraint_when_base_interface_is_not_emitted_Async(bool publicBase)
    {
        static RecordModel CreateRecord(string name)
        {
            return new()
            {
                DescriptionItems = [],
                FieldModels = [],
                MethodModels = [],
                IsAbstract = false,
                UsesIntrusiveReferenceCounting = true,
                ObjectKind = NativeObjectKind.IntrusiveHandle,
                Size = 8,
                Alignment = 8,
                SourceHeader = "test.hxx",
                Type = new() { CppTypeName = name, CSharpPInvokeType = new(name), CSharpPublicType = new(name), },
            };
        }

        var hidden = CreateRecord("HiddenBase");
        var record = CreateRecord("Storage");
        record.Bases =
        [
            new()
            {
                Base = hidden, IsPublic = publicBase, IsVirtual = false,
                PointerAdjustment = PointerAdjustmentKind.Identity,
            },
        ];
        var catalog = new Dictionary<string, RecordModel>(StringComparer.Ordinal) { ["Storage"] = record, };
        var code = await new CSharpGenerator(record, CreateOptions("LayoutProbe"), catalog)
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);
        const string Probe = """
            namespace LayoutProbe
            {
                public static class Probe
                {
                    public static bool Check()
                    {
                        return typeof(TedToolkit.CppBindings.Occt.Handle<Storage>).IsClass
                            && typeof(TedToolkit.CppBindings.Occt.IStandard_Transient).IsAssignableFrom(typeof(Storage));
                    }
                }
            }
            """;
        await LayoutTests.AssertCompiledStorageAsync(code, Probe).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies transient operations support both owning and borrowed handle receivers.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_owning_and_borrowed_handle_receiver_overloads_Async()
    {
        var boolType = new TypeModel()
        {
            CppTypeName = "bool",
            CSharpPInvokeType = DataType.Bool,
            CSharpPublicType = DataType.Bool,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = true,
            MethodModels =
            [
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = true,
                    IsStatic = false,
                    MethodName = "lock",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = boolType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
            ObjectKind = NativeObjectKind.IntrusiveHandle,
            Size = 8,
            SourceHeader = "Geom_Curve.hxx",
            Type = new()
            {
                CppTypeName = "Geom_Curve",
                CSharpPInvokeType = new("Geom_Curve"),
                CSharpPublicType = new("Geom_Curve"),
            },
        };
        NativeExportNameBuilder.Assign(record);

        var code = await new CSharpGenerator(record, CreateOptions(), nativeFunctionIndices: CreateFunctionIndices(record))
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains(
            "public static bool @lock(this global::TedToolkit.CppBindings.Occt.Handle<Geom_Curve> self)");
        await Assert.That(code).Contains(
            "public static bool @lock(this in global::TedToolkit.CppBindings.Occt.handle<Geom_Curve> self)");
        await Assert.That(code).Contains("NativeApi.GetFunction(");
        await Assert.That(code).DoesNotContain("lockCore");
        await Assert.That(code).DoesNotContain("ICppOwner");
        await Assert.That(code.Split("GC.KeepAlive(self);", StringSplitOptions.None).Length - 1).IsEqualTo(1);
        await Assert.That(code.Split("NativeApi.GetFunction(0)", StringSplitOptions.None).Length - 1).IsEqualTo(2);
        await Assert.That(code).DoesNotContain("TReceiver");
    }

    /// <summary>
    /// Verifies only an actual inheritance reuse surface introduces a non-copying generic receiver.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_use_ref_generic_receiver_only_for_inherited_operations_Async()
    {
        var boolType = new TypeModel()
        {
            CppTypeName = "bool",
            CSharpPInvokeType = DataType.Bool,
            CSharpPublicType = DataType.Bool,
        };
        var baseRecord = new RecordModel()
        {
            DescriptionItems = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            MethodModels =
            [
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = true,
                    IsStatic = false,
                    MethodName = "Read",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = boolType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
            ObjectKind = NativeObjectKind.Value,
            Size = 4,
            SourceHeader = "Base.hxx",
            Type = new()
            {
                CppTypeName = "Base",
                CSharpPInvokeType = new("Base"),
                CSharpPublicType = new("Base"),
            },
        };
        var derivedRecord = new RecordModel()
        {
            Bases =
            [
                new BaseRelationModel()
                {
                    Base = baseRecord,
                    IsVirtual = false,
                    PointerAdjustment = PointerAdjustmentKind.Identity,
                },
            ],
            DescriptionItems = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            MethodModels = [],
            ObjectKind = NativeObjectKind.Value,
            Size = 4,
            SourceHeader = "Derived.hxx",
            Type = new()
            {
                CppTypeName = "Derived",
                CSharpPInvokeType = new("Derived"),
                CSharpPublicType = new("Derived"),
            },
        };
        NativeExportNameBuilder.Assign(baseRecord);
        var catalog = new Dictionary<string, RecordModel>(StringComparer.Ordinal)
        {
            [baseRecord.Type.CppTypeName] = baseRecord,
            [derivedRecord.Type.CppTypeName] = derivedRecord,
        };

        var code = await new CSharpGenerator(
                baseRecord,
                CreateOptions(),
                catalog,
                CreateFunctionIndices(baseRecord, derivedRecord))
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("public static bool Read<TReceiver>(this ref TReceiver self)");
        await Assert.That(code).DoesNotContain("this in TReceiver self");
    }

    /// <summary>
    /// Verifies an owning OCCT handle result preserves the native null state.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_nullable_owning_handle_value_returns_Async()
    {
        var handleType = new TypeModel()
        {
            CppTypeName = "opencascade::handle<Standard_Type>",
            CppValueTypeName = "opencascade::handle<Standard_Type>",
            CSharpPInvokeType = new("global::TedToolkit.CppBindings.Occt.handle<Standard_Type>"),
            CSharpPublicType = new("global::TedToolkit.CppBindings.Occt.handle<Standard_Type>"),
            IsIntrusiveHandle = true,
            IsRecord = true,
            IntrusiveHandleElementCppType = "Standard_Type",
            IntrusiveHandleElementType = "Standard_Type",
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = true,
            MethodModels =
            [
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = true,
                    IsStatic = false,
                    MethodName = "DynamicType",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = handleType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
            ObjectKind = NativeObjectKind.IntrusiveHandle,
            Size = 8,
            SourceHeader = "Standard_Transient.hxx",
            Type = new()
            {
                CppTypeName = "Standard_Transient",
                CSharpPInvokeType = new("Standard_Transient"),
                CSharpPublicType = new("Standard_Transient"),
            },
        };
        NativeExportNameBuilder.Assign(record);

        var indices = CreateFunctionIndices(record);
        indices["Standard_Type_Release"] = indices.Count;
        var code = await new CSharpGenerator(record, CreateOptions(), nativeFunctionIndices: indices)
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("Handle<Standard_Type>? DynamicType(");
        await Assert.That(code).Contains("if (__result == null)");
        await Assert.That(code).Contains("return null;");
        await Assert.That(code).Contains("NativeApi.GetFunction(");
    }

    /// <summary>
    /// Verifies borrowed reference results preserve their C++ constness without copying.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_borrowed_reference_returns_without_copying_Async()
    {
        var constReference = CreateReferenceType("ref readonly gp_Pnt2d", valueIsConst: true);
        var mutableReference = CreateReferenceType("ref gp_Pnt2d", valueIsConst: false);
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = true,
            MethodModels =
            [
                CreateReferenceMethod("Pole", constReference, noExceptions: false),
                CreateReferenceMethod("ChangePole", mutableReference, noExceptions: true),
            ],
            ObjectKind = NativeObjectKind.IntrusiveHandle,
            Size = 8,
            SourceHeader = "Curve.hxx",
            Type = new()
            {
                CppTypeName = "Curve",
                CSharpPInvokeType = new("Curve"),
                CSharpPublicType = new("Curve"),
            },
        };
        NativeExportNameBuilder.Assign(record);
        var generator = new CSharpGenerator(
            record,
            CreateOptions(),
            nativeFunctionIndices: CreateFunctionIndices(record));

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains(
            "public static ref readonly gp_Pnt2d Pole(this global::TedToolkit.CppBindings.Occt.Handle<Curve> self)");
        await Assert.That(code).Contains(
            "public static ref gp_Pnt2d ChangePole(this global::TedToolkit.CppBindings.Occt.Handle<Curve> self)");
        await Assert.That(code).DoesNotContain("ref readonly ref readonly");
        await Assert.That(code).DoesNotContain("Owned<gp_Pnt2d>");
        await Assert.That(code).DoesNotContain("new gp_Pnt2d");
        await Assert.That(code).Contains("return ref *__result;");
    }

    /// <summary>
    /// Verifies method comments are projected into generated C# documentation.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_method_summary_parameter_and_return_comments_Async()
    {
        var intType = new TypeModel()
        {
            CppTypeName = "int",
            CSharpPInvokeType = DataType.Int,
            CSharpPublicType = DataType.Int,
        };

        var record = new RecordModel()
        {
            DescriptionItems = [new DescriptionSummary(new DescriptionText("Point wrapper.")),],
            Bases = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            SourceHeader = "gp_Pnt2d.hxx",
            Size = 16,
            Type = new()
            {
                CppTypeName = "gp_Pnt2d",
                CSharpPInvokeType = new("gp_Pnt2d"),
                CSharpPublicType = new("gp_Pnt2d"),
            },
            FieldModels =
                [
                    new FieldModel()
                    {
                        DescriptionItems = [],
                        Offset = 8,
                        Size = 4,
                        Alignment = 4,
                        Name = "myValue",
                        Type = intType,
                    },
                ],
            MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems =
                        [
                            new DescriptionSummary(new DescriptionText("Returns the coordinate of range theIndex.")),
                            new DescriptionRemarks(new DescriptionText("Raises OutOfRange if theIndex != {1, 2}.")),
                        ],
                        ReturnTypeDescriptionItems = [new DescriptionText("Coordinate value."),],
                        NoExceptions = false,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = intType,
                        MethodName = "Coord",
                        Type = MethodModelType.NORMAL,
                        Parameters =
                        [
                            new ParameterModel()
                            {
                                DescriptionItems = [new DescriptionText("Coordinate index."),],
                                Type = intType,
                                Name = "params",
                            },
                        ],
                    },
                ],
        };
        NativeExportNameBuilder.Assign(record);
        var generator = new CSharpGenerator(
            record,
            CreateOptions(),
            nativeFunctionIndices: CreateFunctionIndices(record));

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("Point wrapper.");
        await Assert.That(code).Contains("public static int Coord(this ref gp_Pnt2d self, int @params)");
        await Assert.That(code).Contains("NativeApi.GetFunction(");
        await Assert.That(code).Contains("LayoutKind.Sequential");
        await Assert.That(code).DoesNotContain("FieldOffset");
        await Assert.That(code).Contains("public unsafe struct gp_Pnt2d :");
        await Assert.That(code).Contains("Igp_Pnt2d");
        await Assert.That(code).Contains("public unsafe interface Igp_Pnt2d");
        await Assert.That(code).Contains("private fixed byte __padding0[8];");
        await Assert.That(code).Contains("public int myValue;");
    }

    /// <summary>
    /// Verifies a static helper and an instance operation remain callable when C# ref-kind rules collide.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_number_static_method_when_its_csharp_signature_collides_with_an_instance_method_Async()
    {
        var boolType = new TypeModel()
        {
            CppTypeName = "bool",
            CSharpPInvokeType = DataType.Bool,
            CSharpPublicType = DataType.Bool,
        };
        var matrixReference = new TypeModel()
        {
            CppTypeName = "const Matrix &",
            CppValueTypeName = "Matrix",
            CSharpPInvokeType = new("Matrix*"),
            CSharpPublicType = new("Matrix"),
            IsRecord = true,
            Transport = new(
                valueIsConst: true,
                [new(TypeIndirectionKind.LValueReference, IsConstQualified: false),]),
        };
        ParameterModel Parameter(string name)
        {
            return new()
            {
                DescriptionItems = [],
                Name = name,
                Type = matrixReference,
            };
        }

        var record = new RecordModel()
        {
            DescriptionItems = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            MethodModels =
            [
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = false,
                    IsStatic = true,
                    MethodName = "Multiply",
                    NoExceptions = true,
                    Parameters = [Parameter("left"), Parameter("right"),],
                    ReturnType = boolType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = false,
                    IsStatic = false,
                    MethodName = "Multiply",
                    NoExceptions = true,
                    Parameters = [Parameter("right"),],
                    ReturnType = boolType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
            ObjectKind = NativeObjectKind.Value,
            Size = 8,
            SourceHeader = "Matrix.hxx",
            Type = new()
            {
                CppTypeName = "Matrix",
                CSharpPInvokeType = new("Matrix"),
                CSharpPublicType = new("Matrix"),
            },
        };
        NativeExportNameBuilder.Assign(record);

        var code = await new CSharpGenerator(record, CreateOptions(), nativeFunctionIndices: CreateFunctionIndices(record))
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("public static bool Multiply_1(in Matrix left, in Matrix right)");
        await Assert.That(code).Contains("public static bool Multiply(this ref Matrix self, in Matrix right)");
    }

    /// <summary>
    /// Verifies mixed template projections emit one generic layout and exact closed native operations.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_generic_layout_with_fixed_native_specialization_Async()
    {
        var valueType = new TypeModel()
        {
            CppTypeName = "double",
            CSharpPInvokeType = DataType.Double,
            CSharpPublicType = DataType.Double,
        };
        var record = new RecordModel()
        {
            TemplateProjection = new()
            {
                FixedTypeName = "Buffer_double_4_void",
                NativeTemplateName = "Buffer",
                NativeTypePattern = "Buffer<TValue, 4, void>",
                FamilyName = "Buffer_4_void",
                DeclarationTypeName = "Buffer_4_void<TValue>",
                ClosedTypeName = "Buffer_4_void<double>",
                Arguments =
                [
                    new()
                    {
                        ParameterName = "TValue",
                        NativeArgument = "double",
                        ClosedCSharpType = "double",
                        Kind = TemplateArgumentProjectionKind.Generic,
                    },
                    new()
                    {
                        ParameterName = "Size",
                        NativeArgument = "4",
                        Kind = TemplateArgumentProjectionKind.Fixed,
                    },
                    new()
                    {
                        ParameterName = "TPolicy",
                        NativeArgument = "void",
                        Kind = TemplateArgumentProjectionKind.Fixed,
                    },
                ],
            },
            DescriptionItems = [],
            FieldModels =
            [
                new()
                {
                    Alignment = 8,
                    CppTemplateType = "TValue",
                    CSharpTemplateType = "TValue",
                    DescriptionItems = [],
                    Name = "Value",
                    Offset = 0,
                    Size = 8,
                    Type = valueType,
                },
            ],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            MethodModels =
            [
                new()
                {
                    DescriptionItems = [],
                    IsConst = false,
                    IsStatic = false,
                    MethodName = "Clear",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = new()
                    {
                        CppTypeName = "void",
                        CSharpPInvokeType = new("void"),
                        CSharpPublicType = new("void"),
                    },
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
            ObjectKind = NativeObjectKind.Value,
            Size = 8,
            SourceHeader = "Buffer.hxx",
            Type = new()
            {
                CppTypeName = "Buffer<double, 4, void>",
                CSharpPInvokeType = new("Buffer_4_void<double>"),
                CSharpPublicType = new("Buffer_4_void<double>"),
            },
        };
        NativeExportNameBuilder.Assign(record);

        var code = await new CSharpGenerator(record, CreateOptions(), nativeFunctionIndices: CreateFunctionIndices(record))
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("public unsafe struct Buffer_4_void<");
        await Assert.That(code).Contains("where TValue: unmanaged");
        await Assert.That(code).Contains("public TValue Value;");
        await Assert.That(code).DoesNotContain("Size = 8");
        await Assert.That(code).Contains("class Buffer_double_4_voidExtensions");
        await Assert.That(code).Contains("Clear(this ref Buffer_4_void<double> self)");
    }

    /// <summary>
    /// Verifies owned parameters use their actual runtime and slots use the selected output namespace.
    /// </summary>
    /// <param name="kind">The native ownership category.</param>
    /// <param name="owner">The fully qualified owner type.</param>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    [Arguments(NativeObjectKind.Owned, "global::TedToolkit.CppBindings.Owned")]
    [Arguments(NativeObjectKind.IntrusiveHandle, "global::TedToolkit.CppBindings.Occt.Handle")]
    public async Task Should_separate_runtime_parameter_identity_from_generated_namespace_Async(
        NativeObjectKind kind,
        string owner)
    {
        var type = new TypeModel()
        {
            CppTypeName = "Resource",
            CppValueTypeName = "Resource",
            CSharpPInvokeType = new("Resource"),
            CSharpPublicType = new("Resource"),
            IsRecord = true,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = kind is NativeObjectKind.IntrusiveHandle,
            ObjectKind = kind,
            Size = 8,
            SourceHeader = "Resource.hxx",
            Type = type,
            MethodModels =
            [
                new MethodModel()
                {
                    DescriptionItems = [],
                    IsConst = true,
                    IsStatic = false,
                    MethodName = "Consume",
                    NoExceptions = true,
                    Parameters = [new() { DescriptionItems = [], Name = "other", Type = type, },],
                    ReturnType = new() { CppTypeName = "int", CSharpPInvokeType = DataType.Int, CSharpPublicType = DataType.Int, },
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NORMAL,
                },
            ],
        };
        NativeExportNameBuilder.Assign(record);
        var catalog = new Dictionary<string, RecordModel>(StringComparer.Ordinal) { ["Resource"] = record, };
        var code = await new CSharpGenerator(
                record, CreateOptions("Independent.Generated"), catalog, CreateFunctionIndices(record))
            .GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains(owner + "<Resource> other");
        await Assert.That(code).Contains("global::Independent.Generated.NativeApi.GetFunction(");
        await Assert.That(code).DoesNotContain("global::TedToolkit.CppBindings.Occt.NativeApi");
        await Assert.That(code).DoesNotContain("global::TedToolkit.CppBindings.Occt.Owned");
    }

    private static IOptions<OcctGenerationOptions> CreateOptions(string cSharpNamespace = "TedToolkit.CppBindings.Occt")
    {
        return Microsoft.Extensions.Options.Options.Create(new OcctGenerationOptions()
        {
            DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
            CSharpNamespace = cSharpNamespace,
        });
    }

    private static Dictionary<string, int> CreateFunctionIndices(params RecordModel[] records)
    {
        return NativeExportInventory.GetExports(records, ["NativeError_Clear",])
            .Select(static (export, index) => (export, index))
            .ToDictionary(static value => value.export, static value => value.index, StringComparer.Ordinal);
    }

    private static TypeModel CreateReferenceType(string publicType, bool valueIsConst)
    {
        return new()
        {
            CppTypeName = valueIsConst ? "const gp_Pnt2d &" : "gp_Pnt2d &",
            CppValueTypeName = "gp_Pnt2d",
            CSharpPInvokeType = new(publicType),
            CSharpPublicType = new(publicType),
            IsRecord = true,
            Transport = new(
                valueIsConst,
                [new(TypeIndirectionKind.LValueReference, IsConstQualified: false),]),
        };
    }

    private static MethodModel CreateReferenceMethod(
        string name,
        TypeModel returnType,
        bool noExceptions)
    {
        return new()
        {
            DescriptionItems = [],
            IsConst = true,
            IsStatic = false,
            MethodName = name,
            NoExceptions = noExceptions,
            Parameters = [],
            ReturnType = returnType,
            ReturnTypeDescriptionItems = [],
            Type = MethodModelType.NORMAL,
        };
    }
}