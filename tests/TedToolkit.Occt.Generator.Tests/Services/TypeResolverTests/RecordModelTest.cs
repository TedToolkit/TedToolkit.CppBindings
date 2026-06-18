// -----------------------------------------------------------------------
// <copyright file="TypeModelTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Services.TypeResolverTests;

/// <summary>
/// Tests for the projection-based type model.
/// </summary>
internal sealed class RecordModelTest
{
    /// <summary>
    /// Verifies a type model can hold both C++ interop and C# projection forms.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_store_cppast_and_csharp_projection_types_Async()
    {
        var cppInteropType = new CppPointerType(new CppClass("Geom_Surface"));

        var model = new Generator.Models.RecordModel()
        {
            SourceType = null!,
            CppOriginalDisplayName = "occ::handle<Geom_Surface>",
            CppInteropType = cppInteropType,
            CSharpPInvokeType = new DataType(new PostfixUnaryExpression(new SimpleNameExpression("Geom_Surface"), "*")),
            CSharpPublicType = new DataType(new SimpleNameExpression("Handle_Geom_Surface")),
        };

        await Assert.That(model.CppOriginalDisplayName).IsEqualTo("occ::handle<Geom_Surface>");
        await Assert.That(model.CppInteropType).IsSameReferenceAs(cppInteropType);
        await Assert.That(model.CSharpPInvokeType).IsNotNull();
        await Assert.That(model.CSharpPublicType).IsNotNull();
    }
}
