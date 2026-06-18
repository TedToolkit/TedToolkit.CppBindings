// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.EnumGeneratorTests;

internal sealed class GenerateAsyncTest
{
    [Test]
    public async Task Should_generate_public_csharp_enum_with_underlying_type_and_values_Async()
    {
        var generator = new EnumGenerator(new EnumModel
        {
            Name = "Quantity_TypeOfColor",
            SourceType = "Quantity_TypeOfColor",
            UnderlyingType = new DataType("byte"),
            Members =
            [
                new EnumMemberModel { Name = "Quantity_TypeOfColor_RGB", Value = "1", },
                new EnumMemberModel { Name = "Quantity_TypeOfColor_sRGB", Value = "2", },
            ],
        });

        var code = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(code).Contains("public enum Quantity_TypeOfColor : byte");
        await Assert.That(code).Contains("Quantity_TypeOfColor_RGB = 1");
        await Assert.That(code).Contains("Quantity_TypeOfColor_sRGB = 2");
    }
}
