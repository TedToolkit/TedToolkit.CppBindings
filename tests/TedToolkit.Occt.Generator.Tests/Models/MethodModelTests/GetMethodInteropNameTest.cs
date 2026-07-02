// -----------------------------------------------------------------------
// <copyright file="GetMethodInteropNameTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Models.MethodModelTests;

/// <summary>
/// Verifies <see cref="MethodModel.GetMethodInteropName(RecordModel)"/>.
/// </summary>
internal sealed class GetMethodInteropNameTest
{
    /// <summary>
    /// Verifies interop names drop const qualifiers from parameter type segments.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_strip_const_qualifiers_from_parameter_type_names_Async()
    {
        var recordType = CreateType("gp_Pnt");
        var method = new MethodModel()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = false,
            IsStatic = false,
            ReturnType = CreateType("void"),
            MethodName = "SetCoord",
            Type = MethodModelType.NORMAL,
            Parameters =
            [
                new ParameterModel()
                {
                    DescriptionItems = [],
                    Name = "surface",
                    Type = CreateType("const Geom_Surface&"),
                },
                new ParameterModel()
                {
                    DescriptionItems = [],
                    Name = "text",
                    Type = CreateType("char const *"),
                },
            ],
        };

        var record = new RecordModel()
        {
            DescriptionItems = [],
            Base = null,
            IsAbstract = false,
            SourceHeader = "gp_Pnt.hxx",
            Type = recordType,
            Size = 0,
            FieldModels = [],
            MethodModels = [],
        };

        await Assert.That(method.GetMethodInteropName(record))
            .IsEqualTo("gp_Pnt_SetCoord_Geom_Surface_char");
    }

    private static TypeModel CreateType(string cppTypeName)
    {
        return new()
        {
            CppTypeName = cppTypeName,
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };
    }
}
