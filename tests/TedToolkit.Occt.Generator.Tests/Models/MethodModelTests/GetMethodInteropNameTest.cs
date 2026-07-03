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
    /// Verifies interop names are derived from PInvoke data types.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_use_pinvoke_type_code_for_parameter_type_names_Async()
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
                    Type = CreateType("const Geom_Surface&", new DataType("Geom_Surface").Pointer),
                },
                new ParameterModel()
                {
                    DescriptionItems = [],
                    Name = "text",
                    Type = CreateType("char const *", DataType.Sbyte.Pointer),
                },
            ],
        };

        var record = new RecordModel()
        {
            DescriptionItems = [],
            Base = null,
            IsAbstract = false,
            IsStandardTransient = false,
            RequiresNew = false,
            SourceHeader = "gp_Pnt.hxx",
            Type = recordType,
            Size = 0,
            FieldModels = [],
            MethodModels = [],
        };

        await Assert.That(method.GetMethodInteropName(record))
            .IsEqualTo("gp_Pnt_SetCoord_Geom_Surface_sbyte");
    }

    /// <summary>
    /// Verifies conversion wrappers include the return type in their interop name.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_use_return_type_code_for_conversion_interop_names_Async()
    {
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Base = null,
            IsAbstract = false,
            IsStandardTransient = false,
            RequiresNew = false,
            SourceHeader = "Value.hxx",
            Type = CreateType("Value", new DataType("Value")),
            Size = 0,
            FieldModels = [],
            MethodModels = [],
        };

        var implicitMethod = new MethodModel()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = true,
            IsStatic = false,
            ReturnType = CreateType("bool", DataType.Bool),
            MethodName = "Implicit",
            Type = MethodModelType.IMPLICIT,
            Parameters = [],
        };

        var explicitMethod = new MethodModel()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = true,
            IsStatic = false,
            ReturnType = CreateType("int", DataType.Int),
            MethodName = "Explicit",
            Type = MethodModelType.EXPLICIT,
            Parameters = [],
        };

        await Assert.That(implicitMethod.GetMethodInteropName(record)).IsEqualTo("Value_Implicit_bool");
        await Assert.That(explicitMethod.GetMethodInteropName(record)).IsEqualTo("Value_Explicit_int");
    }

    private static TypeModel CreateType(string cppTypeName, DataType? csharpPInvokeType = null)
    {
        return new()
        {
            CppTypeName = cppTypeName,
            CSharpPInvokeType = csharpPInvokeType ?? DataType.Void,
            CSharpPublicType = DataType.Void,
        };
    }
}