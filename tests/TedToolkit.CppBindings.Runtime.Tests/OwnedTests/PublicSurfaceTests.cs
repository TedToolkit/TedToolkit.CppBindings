// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.OwnedTests;

/// <summary>
/// Verifies the public non-transient RAII owner contract.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that Owned exposes only the approved generated construction, value, and disposal surface.
    /// </summary>
    /// <returns>A task that completes when the public-surface assertions finish.</returns>
    [Test]
    public async Task Should_expose_only_the_approved_owned_surface_Async()
    {
        var ownedDefinition = typeof(Owned<>);
        var ownedType = typeof(Owned<TCollection_TestRaii>);
        var constructor = ownedType.GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var value = ownedType.GetProperty(nameof(Owned<TCollection_TestRaii>.Value));
        var declaredMethods = ownedType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();
        var genericParameter = ownedDefinition.GetGenericArguments().Single();
        var genericParameterAttributes = genericParameter.GenericParameterAttributes;
        var genericConstraints = genericParameter.GetGenericParameterConstraints();
        var genericParameterAttributeNames = genericParameter
            .GetCustomAttributesData()
            .Select(static attribute => attribute.AttributeType.FullName!)
            .ToArray();
        var containedFields = ownedType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(static field => field.FieldType == typeof(TCollection_TestRaii))
            .ToArray();

        await Assert.That(ownedDefinition.IsClass).IsTrue();
        await Assert.That(ownedDefinition.IsSealed).IsTrue();
        await Assert.That(ownedType.GetInterfaces())
            .IsEquivalentTo([typeof(IDisposable), typeof(ICppOwner<TCollection_TestRaii>),]);
        await Assert.That(genericParameterAttributes)
            .IsEqualTo(
                GenericParameterAttributes.NotNullableValueTypeConstraint
                | GenericParameterAttributes.DefaultConstructorConstraint);
        await Assert.That(genericConstraints).IsEquivalentTo([typeof(ValueType), typeof(ICppRaii),]);
        await Assert.That(genericParameterAttributeNames)
            .IsEquivalentTo(["System.Runtime.CompilerServices.IsUnmanagedAttribute",]);

        await Assert.That(parameters.Length).IsEqualTo(1);
        await Assert.That(parameters[0].ParameterType.IsFunctionPointer).IsTrue();
        await Assert.That(parameters[0].ParameterType.IsUnmanagedFunctionPointer).IsTrue();
        await Assert.That(parameters[0].ParameterType).IsEqualTo(GetCdeclDestructorType());
        await Assert.That(parameters[0].ParameterType.GetFunctionPointerReturnType()).IsEqualTo(typeof(void));
        await Assert.That(parameters[0].ParameterType.GetFunctionPointerParameterTypes().Single().IsPointer).IsTrue();
        await Assert.That(parameters[0].ParameterType.GetFunctionPointerParameterTypes().Single().GetElementType())
            .IsEqualTo(typeof(TCollection_TestRaii));
        await Assert.That(constructor.GetCustomAttribute<GeneratedCodeOnlyAttribute>()).IsNotNull();

        await Assert.That(containedFields).Count().IsEqualTo(1);
        await Assert.That(ownedType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Count().IsEqualTo(1);
        await Assert.That(value).IsNotNull();
        await Assert.That(value!.PropertyType).IsEqualTo(typeof(TCollection_TestRaii).MakeByRefType());
        await Assert.That(declaredMethods).IsEquivalentTo(["Dispose", "get_Value",]);
        await Assert.That(ownedType.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .IsEmpty();
        await Assert.That(ownedType.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .IsEmpty();
        await Assert.That(ownedType.BaseType).IsEqualTo(typeof(object));
        await Assert.That(typeof(ICppRaii).GetMembers()).IsEmpty();
        await Assert.That(typeof(ICppOwner<TCollection_TestRaii>).GetProperties().Single().PropertyType)
            .IsEqualTo(typeof(TCollection_TestRaii).MakeByRefType());
    }

    /// <summary>
    /// Verifies that trivial and transient structs cannot close the Owned generic type.
    /// </summary>
    /// <returns>A task that completes when the generic-constraint assertions finish.</returns>
    [Test]
    public async Task Should_reject_non_raii_generic_arguments_Async()
    {
        await Assert.That(CreateTrivialOwnerType).Throws<ArgumentException>();
        await Assert.That(CreateTransientOwnerType).Throws<ArgumentException>();
    }

    private static void CreateTrivialOwnerType()
    {
        _ = typeof(Owned<>).MakeGenericType(typeof(int));
    }

    private static void CreateTransientOwnerType()
    {
        _ = typeof(Owned<>).MakeGenericType(typeof(TestTransient));
    }

    private static unsafe Type GetCdeclDestructorType()
    {
        return typeof(delegate* unmanaged[Cdecl]<TCollection_TestRaii*, void>);
    }

    private struct TCollection_TestRaii : ICppRaii;

    private struct TestTransient : IStandard_Transient;
}