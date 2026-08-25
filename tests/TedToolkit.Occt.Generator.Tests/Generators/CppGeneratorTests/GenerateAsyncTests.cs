// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Generators;
using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Generators.CppGeneratorTests;

/// <summary>
/// Verifies C++ invocation-helper generation directly from parsed record models.
/// </summary>
internal sealed class GenerateAsyncTests
{
    /// <summary>
    /// Verifies that one record produces includes and invocation bodies without a parallel ABI operation model.
    /// </summary>
    /// <returns>A task that completes when the generated source assertions finish.</returns>
    [Test]
    public async Task Should_generate_invocation_helpers_directly_from_record_methods_Async()
    {
        var doubleType = new TypeModel()
        {
            CppTypeName = "double",
            CSharpPInvokeType = DataType.Double,
            CSharpPublicType = DataType.Double,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Base = null,
            IsAbstract = false,
            IsStandardTransient = false,
            SourceHeader = "gp_Pnt2d.hxx",
            Type = new()
            {
                CppTypeName = "gp_Pnt2d",
                CSharpPInvokeType = new("gp_Pnt2d"),
                CSharpPublicType = new("gp_Pnt2d"),
            },
            Size = 16,
            FieldModels = [],
            MethodModels =
            [
                CreateMethod(MethodModelType.NEW, "New", doubleType, [CreateParameter("x", doubleType),]),
                CreateMethod(MethodModelType.NORMAL, "X", doubleType, [], isConst: true),
            ],
        };

        var source = await new CppGenerator(record).GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(source).Contains("#include <gp_Pnt2d.hxx>");
        await Assert.That(source).Contains("gp_Pnt2d* create_0(double x)");
        await Assert.That(source).Contains("return new gp_Pnt2d(x);");
        await Assert.That(source).Contains("double invoke_1(const gp_Pnt2d& self)");
        await Assert.That(source).Contains("return self.X();");
        await Assert.That(source).DoesNotContain("AbiOperationModel");
        await Assert.That(source).DoesNotContain("CSHARP_WRAPPER");
        await Assert.That(source).DoesNotContain("extern \"C\"");
    }

    private static MethodModel CreateMethod(
        MethodModelType type,
        string name,
        TypeModel returnType,
        IReadOnlyList<ParameterModel> parameters,
        bool isConst = false)
    {
        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = false,
            IsConst = isConst,
            IsStatic = false,
            ReturnType = returnType,
            MethodName = name,
            Type = type,
            Parameters = parameters,
        };
    }

    private static ParameterModel CreateParameter(string name, TypeModel type)
    {
        return new()
        {
            DescriptionItems = [],
            Name = name,
            Type = type,
        };
    }
}