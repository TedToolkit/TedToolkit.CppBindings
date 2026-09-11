// -----------------------------------------------------------------------
// <copyright file="CompilerProbeModuleTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Modules;

/// <summary>
/// Verifies native compiler probe result completion.
/// </summary>
internal sealed class CompilerProbeModuleTests
{
    /// <summary>
    /// Verifies compiler traits select Value, Owned, and Handle without caller configuration.
    /// </summary>
    /// <returns>A task that completes when all assertions finish.</returns>
    [Test]
    public async Task Should_classify_records_and_remove_trivial_destruction_Async()
    {
        var value = CreateRecord("Value", 8, isTransient: false, includeDestructor: true);
        var owned = CreateRecord("Owned", 16, isTransient: false, includeDestructor: true);
        var handle = CreateRecord("Handle", 24, isTransient: true, includeDestructor: true);

        OcctCompilerProbeModule.CompleteRecords(
            [value, owned, handle,],
            "0\t8\t8\t1\t1\t1\n1\t16\t8\t0\t0\t1\n2\t24\t8\t0\t0\t1\n");

        await Assert.That(value.ObjectKind).IsEqualTo(NativeObjectKind.Value);
        await Assert.That(value.Alignment).IsEqualTo(8);
        await Assert.That(value.MethodModels).IsEmpty();
        await Assert.That(owned.ObjectKind).IsEqualTo(NativeObjectKind.Owned);
        await Assert.That(owned.MethodModels.Single().NativeExportName).IsEqualTo("Owned_Destroy");
        await Assert.That(handle.ObjectKind).IsEqualTo(NativeObjectKind.IntrusiveHandle);
        await Assert.That(handle.MethodModels.Select(static method => method.NativeExportName))
            .IsEquivalentTo(["Handle_Release", "Handle_Destroy",]);
    }

    /// <summary>
    /// Verifies an accessible implicit destructor receives a native destroy operation.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_add_destroy_for_owned_record_with_implicit_destructor_Async()
    {
        var owned = CreateRecord("Owned", 16, isTransient: false, includeDestructor: false);

        OcctCompilerProbeModule.CompleteRecords([owned,], "0\t16\t8\t0\t0\t1\n");

        await Assert.That(owned.MethodModels.Single().Type).IsEqualTo(MethodModelType.DELETE);
        await Assert.That(owned.MethodModels.Single().NativeExportName).IsEqualTo("Owned_Destroy");
    }

    /// <summary>
    /// Verifies functions absent from the delivered OCCT binaries are removed without removing the type.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_remove_exports_missing_from_delivered_native_binaries_Async()
    {
        var record = CreateRecord("Cocoa_LocalPool", 16, isTransient: false, includeDestructor: true);

        OcctCompilerProbeModule.CompleteRecords([record,], "0\t16\t8\t0\t0\t1\n");

        await Assert.That(record.MethodModels).IsEmpty();
        await Assert.That(record.Type.CppTypeName).IsEqualTo("Cocoa_LocalPool");
    }

    /// <summary>
    /// Verifies a compiler/parser size disagreement blocks generation.
    /// </summary>
    /// <returns>A task that completes when the failure assertion finishes.</returns>
    [Test]
    public async Task Should_reject_layout_disagreement_Async()
    {
        var record = CreateRecord("Value", 8, isTransient: false, includeDestructor: false);

        await Assert.That(() => OcctCompilerProbeModule.CompleteRecords(
                [record,],
                "0\t16\t8\t1\t1\t1\n"))
            .Throws<InvalidOperationException>();
    }

    private static RecordModel CreateRecord(
        string name,
        long size,
        bool isTransient,
        bool includeDestructor)
    {
        var voidType = new TypeModel()
        {
            CppTypeName = "void",
            CppValueTypeName = "void",
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = isTransient,
            SourceHeader = name + ".hxx",
            Type = new()
            {
                CppTypeName = name,
                CppValueTypeName = name,
                CSharpPInvokeType = new(name),
                CSharpPublicType = new(name),
            },
            Size = size,
            FieldModels = [],
            MethodModels = includeDestructor
                ?
                [
                    new MethodModel()
                    {
                        DescriptionItems = [],
                        ReturnTypeDescriptionItems = [],
                        NoExceptions = true,
                        IsConst = false,
                        IsStatic = false,
                        ReturnType = voidType,
                        MethodName = "Delete",
                        Type = MethodModelType.DELETE,
                        Parameters = [],
                    },
                ]
                : [],
        };
        NativeExportNameBuilder.Assign(record);
        return record;
    }
}