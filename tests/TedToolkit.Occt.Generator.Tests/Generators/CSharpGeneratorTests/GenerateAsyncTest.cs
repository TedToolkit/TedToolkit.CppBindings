// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.CSharpGeneratorTests;

/// <summary>
/// Verifies <see cref="CSharpGenerator"/> output.
/// </summary>
internal sealed class GenerateAsyncTest
{
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
            IsStandardTransient = true,
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
            ObjectKind = NativeObjectKind.Handle,
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
            "public static bool @lock(this global::TedToolkit.Occt.IOcctOwner<Geom_Curve> self)");
        await Assert.That(code).Contains(
            "public static bool @lock(this in global::TedToolkit.Occt.handle<Geom_Curve> self)");
        await Assert.That(code).Contains("NativeApi.GetFunction(");
        await Assert.That(code).DoesNotContain("lockCore");
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
            IsStandardTransient = false,
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
            IsStandardTransient = false,
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
            CSharpPInvokeType = new("global::TedToolkit.Occt.handle<Standard_Type>"),
            CSharpPublicType = new("global::TedToolkit.Occt.handle<Standard_Type>"),
            IsOcctHandle = true,
            IsRecord = true,
            OcctHandleElementCppType = "Standard_Type",
            OcctHandleElementType = "Standard_Type",
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            IsStandardTransient = true,
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
            ObjectKind = NativeObjectKind.Handle,
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
            IsStandardTransient = true,
            MethodModels =
            [
                CreateReferenceMethod("Pole", constReference, noExceptions: false),
                CreateReferenceMethod("ChangePole", mutableReference, noExceptions: true),
            ],
            ObjectKind = NativeObjectKind.Handle,
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
            "public static ref readonly gp_Pnt2d Pole(this global::TedToolkit.Occt.IOcctOwner<Curve> self)");
        await Assert.That(code).Contains(
            "public static ref gp_Pnt2d ChangePole(this global::TedToolkit.Occt.IOcctOwner<Curve> self)");
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
            IsStandardTransient = false,
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
        await Assert.That(code).Contains("private byte __padding0;");
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
                ValueIsConst: true,
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
            IsStandardTransient = false,
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

    private static IOptions<GenerationOptions> CreateOptions()
    {
        return Microsoft.Extensions.Options.Options.Create(new GenerationOptions()
        {
            DeclOptions = [],
            CSharpFolder = new(Path.GetTempPath()),
            CppFolder = new(Path.GetTempPath()),
        });
    }

    private static Dictionary<string, int> CreateFunctionIndices(params RecordModel[] records)
    {
        return NativeApiGenerator.GetExports(records)
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