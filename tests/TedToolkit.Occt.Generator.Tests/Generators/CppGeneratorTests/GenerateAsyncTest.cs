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
    /// Verifies compound-assignment wrappers skip the synthetic result out parameter when returning self.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_omit_result_parameter_for_return_self_operator_Async()
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
                        ReturnType = new TypeModel()
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
        await Assert.That(code).Contains("self+=other;");
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
                        ReturnType = new TypeModel()
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
                        ReturnType = new TypeModel()
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
}
