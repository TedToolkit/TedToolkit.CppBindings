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
}
