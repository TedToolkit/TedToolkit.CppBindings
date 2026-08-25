// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
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

        var generator = new CSharpGenerator(
            new RecordModel()
            {
                DescriptionItems = [new DescriptionSummary(new DescriptionText("Point wrapper.")),],
                Base = null,
                IsAbstract = true,
                IsStandardTransient = false,
                SourceHeader = "gp_Pnt2d.hxx",
                Size = 16,
                Type = new()
                {
                    CppTypeName = "gp_Pnt2d",
                    CSharpPInvokeType = new("gp_Pnt2d"),
                    CSharpPublicType = new("gp_Pnt2d"),
                },
                FieldModels = [],
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
                                Name = "theIndex",
                            },
                        ],
                    },
                ],
            },
            Microsoft.Extensions.Options.Options.Create(new GenerationOptions()
            {
                DeclOptions = [],
                CSharpFolder = new(Path.GetTempPath()),
                CppFolder = new(Path.GetTempPath()),
            }));

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("Point wrapper.");
        await Assert.That(code).Contains("Returns the coordinate of range theIndex.");
        await Assert.That(code).Contains("Raises OutOfRange if theIndex != {1, 2}.");
        await Assert.That(code).Contains("<param name=\"theIndex\">Coordinate index.</param>");
        await Assert.That(code).Contains("<returns>Coordinate value.</returns>");
        await Assert.That(code).Contains("public int Coord(");
    }
}