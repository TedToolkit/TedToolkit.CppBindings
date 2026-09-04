// -----------------------------------------------------------------------
// <copyright file="NativeApiGeneratorTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.CppBindings.Occt.Generator.Models.Declarations;
using TedToolkit.CppBindings.Occt.Generator.Models.Types;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CSharpGeneratorTests;

/// <summary>
/// Verifies the generated process-lifetime native export table.
/// </summary>
internal sealed class NativeApiGeneratorTests
{
    /// <summary>
    /// Verifies native exports distinguish pointer-bearing template arguments.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_preserve_template_pointer_identity_in_native_exports_Async()
    {
        var valueRecord = CreateRecord("NCollection_Allocator<NCollection_Mat4<float>>");
        var pointerRecord = CreateRecord("NCollection_Allocator<NCollection_Mat4<float> *>");

        NativeExportNameBuilder.Assign(valueRecord);
        NativeExportNameBuilder.Assign(pointerRecord);

        await Assert.That(valueRecord.MethodModels.Single().NativeExportName)
            .IsEqualTo("NCollection_Allocator_NCollection_Mat4_float_Create");
        await Assert.That(pointerRecord.MethodModels.Single().NativeExportName)
            .IsEqualTo("NCollection_Allocator_NCollection_Mat4_float_Ptr_Create");
    }

    /// <summary>
    /// Verifies OCCT operations and error cleanup enter one deterministic export inventory.
    /// </summary>
    /// <returns>A task that completes when the source assertions finish.</returns>
    [Test]
    public async Task Should_share_ordered_operation_and_error_exports_Async()
    {
        var voidType = new TypeModel()
        {
            CppTypeName = "void",
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            IsStandardTransient = false,
            MethodModels =
            [
                new()
                {
                    DescriptionItems = [],
                    IsConst = false,
                    IsStatic = false,
                    MethodName = "Destroy",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = voidType,
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.DELETE,
                },
            ],
            ObjectKind = NativeObjectKind.Owned,
            Size = 8,
            SourceHeader = "Thing.hxx",
            Type = new()
            {
                CppTypeName = "Thing",
                CSharpPInvokeType = new("Thing"),
                CSharpPublicType = new("Thing"),
            },
        };
        NativeExportNameBuilder.Assign(record);

        var exports = NativeExportInventory.GetExports([record,]);

        await Assert.That(string.Join(",", exports)).IsEqualTo("NativeError_Clear,Thing_Destroy");
    }

    private static RecordModel CreateRecord(string cppTypeName)
    {
        return new()
        {
            DescriptionItems = [],
            Bases = [],
            FieldModels = [],
            IsAbstract = false,
            IsStandardTransient = false,
            MethodModels =
            [
                new()
                {
                    DescriptionItems = [],
                    IsConst = false,
                    IsStatic = true,
                    MethodName = "Create",
                    NoExceptions = true,
                    Parameters = [],
                    ReturnType = new()
                    {
                        CppTypeName = "void",
                        CSharpPInvokeType = DataType.Void,
                        CSharpPublicType = DataType.Void,
                    },
                    ReturnTypeDescriptionItems = [],
                    Type = MethodModelType.NEW,
                },
            ],
            ObjectKind = NativeObjectKind.Owned,
            Size = 1,
            SourceHeader = "Test.hxx",
            Type = new()
            {
                CppTypeName = cppTypeName,
                CSharpPInvokeType = new(cppTypeName.ToGeneratedTypeName()),
                CSharpPublicType = new(cppTypeName.ToGeneratedTypeName()),
            },
        };
    }
}