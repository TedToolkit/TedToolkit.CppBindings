// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.CppGeneratorTests;

/// <summary>
/// Verifies <see cref="CppGenerator"/> output.
/// </summary>
internal sealed class GenerateAsyncTest
{
    /// <summary>
    /// Verifies the generated translation unit directly includes the record declaration header.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_include_record_declaration_header_instead_of_aggregate_header_Async()
    {
        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "gp_Pnt.hxx",
                Size = 0,
                Type = new()
                {
                    CppTypeName = "gp_Pnt",
                    CSharpPInvokeType = new("gp_Pnt"),
                    CSharpPublicType = new("gp_Pnt"),
                },
                FieldModels = [],
                MethodModels = [],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("#include <gp_Pnt.hxx>");
        await Assert.That(code).DoesNotContain("#include \"headers.h\"");
    }

    /// <summary>
    /// Verifies the generated translation unit includes headers required by referenced types, including template arguments.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_include_referenced_type_headers_Async()
    {
        var elementType = new TypeModel()
        {
            CppTypeName = "gp_Pnt2d",
            CSharpPInvokeType = new("gp_Pnt2d"),
            CSharpPublicType = new("gp_Pnt2d"),
            RequiredHeaders = ["gp_Pnt2d.hxx",],
        };

        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "NCollection_Array1.hxx",
                Size = 0,
                Type = new()
                {
                    CppTypeName = "NCollection_Array1<gp_Pnt2d>",
                    CSharpPInvokeType = new("NCollection_Array1_gp_Pnt2d"),
                    CSharpPublicType = new("NCollection_Array1_gp_Pnt2d"),
                    RequiredHeaders = ["gp_Pnt2d.hxx",],
                },
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = true,
                        IsStatic = false,
                        ReturnType = elementType,
                        ReturnSelf = false,
                        MethodName = "Value",
                        Type = MethodModelType.NORMAL,
                        Parameters = [],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("#include <NCollection_Array1.hxx>");
        await Assert.That(code).Contains("#include <gp_Pnt2d.hxx>");
        await Assert.That(code.IndexOf("#include <NCollection_Array1.hxx>", StringComparison.Ordinal))
            .IsLessThan(code.IndexOf("#include <gp_Pnt2d.hxx>", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies compound-assignment wrappers skip the synthetic result out parameter and emit direct operator calls.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_emit_direct_call_for_return_self_operator_Async()
    {
        var valueType = new TypeModel()
        {
            CppTypeName = "Value",
            CSharpPInvokeType = new("Value"),
            CSharpPublicType = new("Value"),
        };

        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "Value.hxx",
                Size = 0,
                Type = valueType,
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "Value &",
                            CSharpPInvokeType = new DataType("Value").Pointer,
                            CSharpPublicType = new("Value"),
                        },
                        ReturnSelf = true,
                        MethodName = "+=",
                        Type = MethodModelType.OPERATOR,
                        Parameters =
                        [
                            new ParameterModel()
                            {
                                DescriptionItems = [],
                                Type = valueType,
                                Name = "other",
                            },
                        ],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("Value & self, Value other), {");
        await Assert.That(code).DoesNotContain("result");
        await Assert.That(code).Contains("self.operator+=(other);");
        await Assert.That(code).DoesNotContain("(self.operator+=(other));");
    }

    /// <summary>
    /// Verifies value-returning operator wrappers emit direct operator calls without redundant outer parentheses.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_emit_direct_call_for_value_returning_operator_Async()
    {
        var valueType = new TypeModel()
        {
            CppTypeName = "Value",
            CSharpPInvokeType = new("Value"),
            CSharpPublicType = new("Value"),
        };

        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "Value.hxx",
                Size = 0,
                Type = valueType,
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = true,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "Value",
                            CSharpPInvokeType = new("Value"),
                            CSharpPublicType = new("Value"),
                        },
                        ReturnSelf = false,
                        MethodName = "+",
                        Type = MethodModelType.OPERATOR,
                        Parameters =
                        [
                            new ParameterModel()
                            {
                                DescriptionItems = [],
                                Type = valueType,
                                Name = "other",
                            },
                        ],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("*result = self.operator+(other);");
        await Assert.That(code).DoesNotContain("*result = (self.operator+(other));");
    }

    /// <summary>
    /// Verifies reference-returning operator wrappers take the address of the direct operator call without redundant parentheses.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_emit_direct_address_of_reference_returning_operator_Async()
    {
        var valueType = new TypeModel()
        {
            CppTypeName = "Value",
            CSharpPInvokeType = new("Value"),
            CSharpPublicType = new("Value"),
        };

        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "Value.hxx",
                Size = 0,
                Type = valueType,
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = true,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "const Value &",
                            CSharpPInvokeType = new DataType("Value").Pointer,
                            CSharpPublicType = new("Value"),
                        },
                        ReturnSelf = false,
                        MethodName = "*",
                        Type = MethodModelType.OPERATOR,
                        Parameters =
                        [
                            new ParameterModel()
                            {
                                DescriptionItems = [],
                                Type = valueType,
                                Name = "other",
                            },
                        ],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("*result = &self.operator*(other);");
        await Assert.That(code).DoesNotContain("*result = &(self.operator*(other));");
        await Assert.That(code).DoesNotContain("*result = &self*other;");
    }

    /// <summary>
    /// Verifies conversion operators generate callable wrapper bodies and stable method names.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_conversion_operator_wrappers_Async()
    {
        var recordType = new TypeModel()
        {
            CppTypeName = "Value",
            CSharpPInvokeType = new("Value"),
            CSharpPublicType = new("Value"),
        };

        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = false,
                SourceHeader = "Value.hxx",
                Size = 0,
                Type = recordType,
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = true,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "bool",
                            CSharpPInvokeType = DataType.Bool,
                            CSharpPublicType = DataType.Bool,
                        },
                        MethodName = "Implicit",
                        Type = MethodModelType.IMPLICIT,
                        Parameters = [],
                    },
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = true,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "int",
                            CSharpPInvokeType = DataType.Int,
                            CSharpPublicType = DataType.Int,
                        },
                        MethodName = "Explicit",
                        Type = MethodModelType.EXPLICIT,
                        Parameters = [],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("CSHARP_WRAPPER(Value_Implicit_bool(const Value & self, bool * const result), {");
        await Assert.That(code).Contains("*result = self.operator bool();");
        await Assert.That(code).Contains("CSHARP_WRAPPER(Value_Explicit_int(const Value & self, int * const result), {");
        await Assert.That(code).Contains("*result = self.operator int();");
    }

    /// <summary>
    /// Verifies heap allocation and transient ref-counting are controlled by dedicated record flags.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_use_record_flags_for_allocation_and_transient_ref_counting_Async()
    {
        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = true,
                RequiresNew = true,
                SourceHeader = "Transient.hxx",
                Size = 0,
                Type = new()
                {
                    CppTypeName = "Transient",
                    CSharpPInvokeType = new("Transient"),
                    CSharpPublicType = new("Transient"),
                },
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "void",
                            CSharpPInvokeType = DataType.Void,
                            CSharpPublicType = DataType.Void,
                        },
                        MethodName = "New",
                        Type = MethodModelType.NEW,
                        Parameters = [],
                    },
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "void",
                            CSharpPInvokeType = DataType.Void,
                            CSharpPublicType = DataType.Void,
                        },
                        MethodName = "Delete",
                        Type = MethodModelType.DELETE,
                        Parameters = [],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("Transient*& instance");
        await Assert.That(code).Contains("instance = new Transient();");
        await Assert.That(code).Contains("instance->IncrementRefCounter();");
        await Assert.That(code).Contains("Transient* instance");
        await Assert.That(code).Contains("delete instance;");
    }

    /// <summary>
    /// Verifies rvalue-reference constructor arguments are forwarded with <c>std::move</c>.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_move_rvalue_reference_constructor_arguments_Async()
    {
        var generator = new CppGenerator(
            new RecordModel()
            {
                DescriptionItems = [],
                Base = null,
                IsAbstract = false,
                IsStandardTransient = false,
                RequiresNew = true,
                SourceHeader = "sstream",
                Size = 0,
                Type = new()
                {
                    CppTypeName = "std::basic_stringstream<char>",
                    CSharpPInvokeType = new("std_basic_stringstream_char"),
                    CSharpPublicType = new("std_basic_stringstream_char"),
                },
                FieldModels = [],
                MethodModels =
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = false,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = new()
                        {
                            CppTypeName = "void",
                            CSharpPInvokeType = DataType.Void,
                            CSharpPublicType = DataType.Void,
                        },
                        MethodName = "New",
                        Type = MethodModelType.NEW,
                        Parameters =
                        [
                            new ParameterModel()
                            {
                                DescriptionItems = [],
                                Type = new()
                                {
                                    CppTypeName = "std::basic_stringstream<char> &&",
                                    CSharpPInvokeType = new DataType("std_basic_stringstream_char").Pointer,
                                    CSharpPublicType = new("std_basic_stringstream_char"),
                                },
                                Name = "_Right",
                            },
                        ],
                    },
                ],
            });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("instance = new std::basic_stringstream<char>(std::move(_Right));");
        await Assert.That(code).DoesNotContain("instance = new std::basic_stringstream<char>(_Right);");
    }
}