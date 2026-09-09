// -----------------------------------------------------------------------
// <copyright file="GenerateAsyncTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;
using TedToolkit.CppBindings.Occt.Generator.Generators;
using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Generators.CppGeneratorTests;

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
        var intType = new TypeModel()
        {
            CppTypeName = "int",
            CSharpPInvokeType = DataType.Int,
            CSharpPublicType = DataType.Int,
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            ObjectKind = NativeObjectKind.Value,
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
                CreateMethod(MethodModelType.NORMAL, "Set", doubleType, [CreateParameter("value", doubleType),]),
                CreateMethod(MethodModelType.NORMAL, "Set", doubleType, [CreateParameter("value", intType),]),
                CreateMethod(MethodModelType.NORMAL, "NoThrow", doubleType, [], noExceptions: true),
            ],
        };

        NativeExportNameBuilder.Assign(record);

        var source = await new CppGenerator(record).GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(source).Contains("#include <gp_Pnt2d.hxx>");
        await Assert.That(source).Contains(
            "extern \"C\" void gp_Pnt2d_Create(gp_Pnt2d* result, double x, NativeError* __error) noexcept");
        await Assert.That(source).Contains("::new (result) gp_Pnt2d(x);");
        await Assert.That(source).Contains(
            "extern \"C\" double gp_Pnt2d_X(const gp_Pnt2d* self, NativeError* __error) noexcept");
        await Assert.That(source).Contains("return (self->*static_cast<double (gp_Pnt2d::*)() const>(&gp_Pnt2d::X))();");
        await Assert.That(source).Contains("catch (const Standard_Failure& exception)");
        await Assert.That(source).Contains("NativeError_Set(__error, 9, \"Standard_Failure\"");
        await Assert.That(source).Contains("NativeError_Set(__error, 8, \"std::exception\"");
        await Assert.That(source).Contains(
            "extern \"C\" double gp_Pnt2d_NoThrow(gp_Pnt2d* self) noexcept");
        await Assert.That(source).Contains("gp_Pnt2d_Set_1");
        await Assert.That(source).Contains("gp_Pnt2d_Set_2");
        await Assert.That(source).DoesNotContain("gp_Pnt2d_Set_double");
        await Assert.That(source).DoesNotContain("AbiOperationModel");
        await Assert.That(source).DoesNotContain("CSHARP_WRAPPER");
        await Assert.That(source).DoesNotContain("namespace ");
        await Assert.That(source).DoesNotContain("create_0");
        await Assert.That(source).DoesNotContain("invoke_1");
    }

    /// <summary>
    /// Verifies an intrusive handle returned by value is retained after the native call.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_generate_handle_value_return_after_the_parameter_list_Async()
    {
        var handleType = new TypeModel()
        {
            CppTypeName = "opencascade::handle<Geom_Surface>",
            CSharpPInvokeType = new("Geom_Surface*"),
            CSharpPublicType = new("Handle<Geom_Surface>"),
            IsIntrusiveHandle = true,
            IsRecord = true,
            IntrusiveHandleElementCppType = "Geom_Surface",
            IntrusiveHandleElementType = "Geom_Surface",
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            ObjectKind = NativeObjectKind.Value,
            SourceHeader = "SurfaceOwner.hxx",
            Type = new()
            {
                CppTypeName = "SurfaceOwner",
                CSharpPInvokeType = new("SurfaceOwner"),
                CSharpPublicType = new("SurfaceOwner"),
            },
            Size = 8,
            FieldModels = [],
            MethodModels =
            [
                CreateMethod(MethodModelType.NORMAL, "Surface", handleType, [], isConst: true),
            ],
        };
        NativeExportNameBuilder.Assign(record);

        var source = await new CppGenerator(record).GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(source).Contains(
            "extern \"C\" Geom_Surface* SurfaceOwner_Surface(const SurfaceOwner* self, NativeError* __error) noexcept");
        await Assert.That(source).Contains(
            "auto resultHandle = (self->*static_cast<::opencascade::handle<Geom_Surface> (SurfaceOwner::*)() const>(&SurfaceOwner::Surface))();");
        await Assert.That(source).Contains("auto* result = resultHandle.get();");
        await Assert.That(source).DoesNotContain("selfauto resultHandle");
    }

    /// <summary>
    /// Verifies omitted native defaults are emitted explicitly after overload-priority selection.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Should_append_native_defaults_to_disambiguate_constructor_Async()
    {
        var intType = new TypeModel()
        {
            CppTypeName = "int",
            CSharpPInvokeType = DataType.Int,
            CSharpPublicType = DataType.Int,
        };
        var method = new MethodModel()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NativeDefaultArguments = ["256",],
            OverloadPriority = 2,
            NoExceptions = false,
            IsConst = false,
            IsStatic = false,
            ReturnType = intType,
            MethodName = "New",
            Type = MethodModelType.NEW,
            Parameters = [CreateParameter("aN", intType),],
        };
        var record = new RecordModel()
        {
            DescriptionItems = [],
            Bases = [],
            IsAbstract = false,
            UsesIntrusiveReferenceCounting = false,
            ObjectKind = NativeObjectKind.Owned,
            SourceHeader = "IntPolyh_Array.hxx",
            Type = new()
            {
                CppTypeName = "IntPolyh_Array<IntPolyh_StartPoint>",
                CSharpPInvokeType = new("IntPolyh_Array_IntPolyh_StartPoint"),
                CSharpPublicType = new("IntPolyh_Array_IntPolyh_StartPoint"),
            },
            Size = 88,
            FieldModels = [],
            MethodModels = [method,],
        };
        NativeExportNameBuilder.Assign(record);

        var source = await new CppGenerator(record).GenerateAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(source).Contains(
            "::new (result) IntPolyh_Array<IntPolyh_StartPoint>(aN, 256);");
    }

    private static MethodModel CreateMethod(
        MethodModelType type,
        string name,
        TypeModel returnType,
        IReadOnlyList<ParameterModel> parameters,
        bool isConst = false,
        bool noExceptions = false)
    {
        return new()
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = noExceptions,
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