// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.HandleTests;

/// <summary>
/// Verifies the public transient-owner contract.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that lowercase handle is a pointer-sized non-owning native layout view.
    /// </summary>
    /// <returns>A task that completes when the public-surface assertions finish.</returns>
    [Test]
    public async Task Should_expose_pointer_sized_non_owning_handle_value_Async()
    {
        var handleType = typeof(handle<TestTransient>);
        var field = handleType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var value = handleType.GetProperty(nameof(handle<TestTransient>.Value));

        await Assert.That(handleType.IsValueType).IsTrue();
        await Assert.That(handleType.IsByRefLike).IsFalse();
        await Assert.That(typeof(IDisposable).IsAssignableFrom(handleType)).IsFalse();
        await Assert.That(handleType.StructLayoutAttribute?.Value).IsEqualTo(LayoutKind.Sequential);
        await Assert.That(Unsafe.SizeOf<handle<TestTransient>>()).IsEqualTo(IntPtr.Size);
        await Assert.That(RuntimeHelpers.IsReferenceOrContainsReferences<handle<TestTransient>>()).IsFalse();
        await Assert.That(field.FieldType.IsPointer).IsTrue();
        await Assert.That(field.FieldType.GetElementType()).IsEqualTo(typeof(TestTransient));
        await Assert.That(value).IsNotNull();
        await Assert.That(value!.PropertyType).IsEqualTo(typeof(TestTransient).MakeByRefType());
        await Assert.That(handleType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)).IsEquivalentTo(["get_Value",]);
    }

    /// <summary>
    /// Verifies that Handle exposes only the approved public construction, value, and disposal surface.
    /// </summary>
    /// <returns>A task that completes when the public-surface assertions finish.</returns>
    [Test]
    public async Task Should_expose_only_the_approved_handle_surface_Async()
    {
        var handleDefinition = typeof(Handle<>);
        var handleType = typeof(Handle<TestTransient>);
        var constructor = handleType.GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var value = handleType.GetProperty(nameof(Handle<TestTransient>.Value));
        var declaredMethods = handleType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();
        var genericParameter = handleDefinition.GetGenericArguments().Single();
        var genericConstraints = genericParameter.GetGenericParameterConstraints();

        await Assert.That(handleDefinition.IsClass).IsTrue();
        await Assert.That(handleDefinition.IsSealed).IsTrue();
        await Assert.That(handleType.GetInterfaces())
            .IsEquivalentTo([typeof(IDisposable), typeof(IOcctOwner<TestTransient>),]);
        await Assert.That(genericParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask)
            .IsEqualTo(GenericParameterAttributes.None);
        await Assert.That(genericParameter.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint)
            .IsEqualTo(GenericParameterAttributes.NotNullableValueTypeConstraint);
        await Assert.That(genericConstraints).Contains(typeof(IStandard_Transient));
        await Assert.That(genericConstraints).Contains(typeof(ValueType));

        await Assert.That(parameters.Length).IsEqualTo(2);
        await Assert.That(parameters[0].ParameterType.IsPointer).IsTrue();
        await Assert.That(parameters[0].ParameterType.GetElementType()).IsEqualTo(typeof(TestTransient));
        await Assert.That(parameters[1].ParameterType.IsFunctionPointer).IsTrue();
        await Assert.That(parameters[1].ParameterType.GetFunctionPointerReturnType()).IsEqualTo(typeof(void));
        await Assert.That(parameters[1].ParameterType.GetFunctionPointerParameterTypes().Single().IsPointer).IsTrue();
        await Assert.That(parameters[1].ParameterType.GetFunctionPointerParameterTypes().Single().GetElementType())
            .IsEqualTo(typeof(TestTransient));
        await Assert.That(handleType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Count().IsEqualTo(1);
        await Assert.That(value).IsNotNull();
        await Assert.That(value!.PropertyType).IsEqualTo(typeof(TestTransient).MakeByRefType());
        await Assert.That(declaredMethods).IsEquivalentTo(["Dispose", "get_Value",]);
        await Assert.That(handleType.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .IsEmpty();
        await Assert.That(handleType.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .IsEmpty();
        await Assert.That(typeof(IStandard_Transient).GetMembers()).IsEmpty();
        await Assert.That(typeof(IOcctOwner<TestTransient>).GetProperties().Single().PropertyType)
            .IsEqualTo(typeof(TestTransient).MakeByRefType());
    }

    private struct TestTransient : IStandard_Transient;
}