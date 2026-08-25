// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.EnumGeneratorTests;

/// <summary>
/// Verifies generated enum output.
/// </summary>
internal sealed class GenerateAsyncTest
{
    /// <summary>
    /// Verifies the generator emits a documented public enum with the configured values.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_generate_public_csharp_enum_with_underlying_type_and_values_Async()
    {
        var generator = new EnumGenerator(new EnumModel()
        {
            DescriptionItems = [new DescriptionSummary(new DescriptionText("Color kind.")),],
            Name = "Quantity_TypeOfColor",
            SourceType = "Quantity_TypeOfColor",
            UnderlyingType = new("byte"),
            Members =
            [
                new EnumMemberModel()
                {
                    DescriptionItems = [new DescriptionSummary(new DescriptionText("RGB space.")),],
                    Name = "Quantity_TypeOfColor_RGB",
                    Value = 1.ToLiteral(),
                },
                new EnumMemberModel()
                {
                    DescriptionItems = [new DescriptionSummary(new DescriptionText("sRGB space.")),],
                    Name = "Quantity_TypeOfColor_sRGB",
                    Value = 2.ToLiteral(),
                },
            ],
        });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("<summary>");
        await Assert.That(code).Contains("Color kind.");
        await Assert.That(code).Contains("public enum Quantity_TypeOfColor : byte");
        await Assert.That(code).Contains("RGB space.");
        await Assert.That(code).Contains("Quantity_TypeOfColor_RGB = 1");
        await Assert.That(code).Contains("Quantity_TypeOfColor_sRGB = 2");
    }
}