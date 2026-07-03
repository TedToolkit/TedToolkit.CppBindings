// -----------------------------------------------------------------------
// <copyright file="GetInvokeNameTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Models.MethodModelTests;

/// <summary>
/// Verifies <see cref="MethodModel.GetInvokeName"/> normalization.
/// </summary>
internal sealed class GetInvokeNameTest
{
    /// <summary>
    /// Verifies normal method names remain unchanged.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_keep_normal_method_name_Async()
    {
        var model = CreateMethod(MethodModelType.NORMAL, "Coord");

        await Assert.That(model.GetInvokeName()).IsEqualTo("Coord");
    }

    /// <summary>
    /// Verifies operator symbols are normalized into identifier-safe invoke names.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_normalize_operator_symbols_Async()
    {
        var model = CreateMethod(MethodModelType.OPERATOR, "operator==");

        await Assert.That(model.GetInvokeName()).IsEqualTo("operatorEqualEqual");
    }

    /// <summary>
    /// Verifies conversion operator spacing is normalized.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_normalize_conversion_operator_spacing_Async()
    {
        var model = CreateMethod(MethodModelType.EXPLICIT, "Explicit");

        await Assert.That(model.GetInvokeName()).IsEqualTo("Explicit");
    }

    private static MethodModel CreateMethod(MethodModelType type, string methodName)
    {
        var voidType = new TypeModel()
        {
            CppTypeName = "void",
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };

        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = false,
            IsStatic = false,
            ReturnType = voidType,
            MethodName = methodName,
            Type = type,
            Parameters = [],
        };
    }
}