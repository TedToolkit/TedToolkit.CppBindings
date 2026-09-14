// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.Serialization;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.OcctExceptionTests;

/// <summary>
/// Verifies the Shared native and OCCT-local managed exception contracts.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that the private native discriminator is not exposed as a public managed type.
    /// </summary>
    /// <returns>A task that completes when the type assertion finishes.</returns>
    [Test]
    public async Task Should_not_expose_a_public_native_error_kind_Async()
    {
        var runtimeAssembly = typeof(OcctException).Assembly;

        await Assert.That(runtimeAssembly.GetType("TedToolkit.CppBindings.OcctErrorKind")).IsNull();
    }

    /// <summary>
    /// Verifies that each exception has the approved .NET base type and diagnostic interface.
    /// </summary>
    /// <returns>A task that completes when the inheritance assertions finish.</returns>
    [Test]
    public async Task Should_expose_the_approved_exception_inheritance_Async()
    {
        var cases = new (Type ExceptionType, Type BaseType)[]
        {
            (typeof(NativeException), typeof(Exception)),
            (typeof(NativeArgumentException), typeof(ArgumentException)),
            (typeof(NativeArgumentOutOfRangeException), typeof(ArgumentOutOfRangeException)),
            (typeof(NativeArithmeticException), typeof(ArithmeticException)),
            (typeof(NativeInvalidOperationException), typeof(InvalidOperationException)),
            (typeof(NativeNullObjectException), typeof(NativeException)),
            (typeof(NativeOutOfMemoryException), typeof(OutOfMemoryException)),
            (typeof(NativeOverflowException), typeof(OverflowException)),
            (typeof(NativeStandardException), typeof(NativeException)),
            (typeof(NativeUnknownException), typeof(NativeException)),
            (typeof(OcctException), typeof(Exception)),
            (typeof(OcctFailureException), typeof(OcctException)),
        };

        foreach (var testCase in cases)
        {
            await Assert.That(testCase.ExceptionType.BaseType).IsEqualTo(testCase.BaseType);
            await Assert.That(typeof(INativeException).IsAssignableFrom(testCase.ExceptionType)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that consumers can catch the types but cannot construct or derive Runtime instances.
    /// </summary>
    /// <returns>A task that completes when the construction assertions finish.</returns>
    [Test]
    public async Task Should_keep_native_origin_construction_inside_runtime_Async()
    {
        Type[] exceptionTypes =
        {
            typeof(NativeException),
            typeof(NativeArgumentException),
            typeof(NativeArgumentOutOfRangeException),
            typeof(NativeArithmeticException),
            typeof(NativeInvalidOperationException),
            typeof(NativeNullObjectException),
            typeof(NativeOutOfMemoryException),
            typeof(NativeOverflowException),
            typeof(NativeStandardException),
            typeof(NativeUnknownException),
            typeof(OcctException),
            typeof(OcctFailureException),
        };

        foreach (var exceptionType in exceptionTypes)
        {
            var externallyVisibleConstructors = exceptionType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(static constructor => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly)
                .ToArray();

            await Assert.That(externallyVisibleConstructors).IsEmpty();
            await Assert.That(exceptionType.GetCustomAttribute<SerializableAttribute>()).IsNull();
            await Assert.That(exceptionType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    [typeof(SerializationInfo), typeof(StreamingContext),],
                    null))
                .IsNull();
        }

        await Assert.That(typeof(OcctException).IsSealed).IsFalse();
        await Assert.That(typeof(OcctException).IsAbstract).IsTrue();
        await Assert.That(typeof(NativeException).IsSealed).IsFalse();
        await Assert.That(typeof(NativeException).IsAbstract).IsTrue();
        foreach (var leafType in exceptionTypes.Where(static type =>
                     type != typeof(NativeException) && type != typeof(OcctException)))
        {
            await Assert.That(leafType.IsSealed).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that the common diagnostic interface contains only the approved read-only properties.
    /// </summary>
    /// <returns>A task that completes when the interface assertions finish.</returns>
    [Test]
    public async Task Should_expose_only_the_approved_diagnostic_properties_Async()
    {
        var properties = typeof(INativeException).GetProperties();

        await Assert.That(properties.Select(static property => property.Name))
            .IsEquivalentTo(["NativeTypeName", "NativeStackTrace",]);
        await Assert.That(properties.All(static property => property.CanRead && !property.CanWrite)).IsTrue();
        await Assert.That(properties.Single(static property => property.Name == "NativeTypeName").PropertyType)
            .IsEqualTo(typeof(string));

        await Assert.That(typeof(IOcctException).GetInterfaces()).Contains(typeof(INativeException));
        await Assert.That(properties.Single(static property => property.Name == "NativeStackTrace").PropertyType)
            .IsEqualTo(typeof(string));
    }
}