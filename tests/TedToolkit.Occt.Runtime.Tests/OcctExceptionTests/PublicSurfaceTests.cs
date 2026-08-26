// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.Serialization;

using TedToolkit.Occt;

namespace TedToolkit.Occt.Runtime.Tests.OcctExceptionTests;

/// <summary>
/// Verifies the public managed OCCT exception contract.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that the public error-kind values match the native contract exactly.
    /// </summary>
    /// <returns>A task that completes when the enum assertions finish.</returns>
    [Test]
    public async Task Should_expose_the_approved_error_kind_values_Async()
    {
        var actual = Enum.GetValues<OcctErrorKind>()
            .Select(static value => (value.ToString(), (int)value))
            .ToArray();
        var expected = new (string Name, int Value)[]
        {
            (nameof(OcctErrorKind.None), 0),
            (nameof(OcctErrorKind.Argument), 1),
            (nameof(OcctErrorKind.ArgumentOutOfRange), 2),
            (nameof(OcctErrorKind.Arithmetic), 3),
            (nameof(OcctErrorKind.InvalidOperation), 4),
            (nameof(OcctErrorKind.NullObject), 5),
            (nameof(OcctErrorKind.OutOfMemory), 6),
            (nameof(OcctErrorKind.Overflow), 7),
            (nameof(OcctErrorKind.OcctFailure), 8),
            (nameof(OcctErrorKind.StandardException), 9),
            (nameof(OcctErrorKind.Unknown), 255),
        };

        await Assert.That(Enum.GetUnderlyingType(typeof(OcctErrorKind))).IsEqualTo(typeof(int));
        await Assert.That(actual).IsEquivalentTo(expected);
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
            (typeof(OcctException), typeof(Exception)),
            (typeof(OcctArgumentException), typeof(ArgumentException)),
            (typeof(OcctArgumentOutOfRangeException), typeof(ArgumentOutOfRangeException)),
            (typeof(OcctArithmeticException), typeof(ArithmeticException)),
            (typeof(OcctInvalidOperationException), typeof(InvalidOperationException)),
            (typeof(OcctNullObjectException), typeof(OcctException)),
            (typeof(OcctOutOfMemoryException), typeof(OutOfMemoryException)),
            (typeof(OcctOverflowException), typeof(OverflowException)),
        };

        foreach (var testCase in cases)
        {
            await Assert.That(testCase.ExceptionType.BaseType).IsEqualTo(testCase.BaseType);
            await Assert.That(typeof(IOcctException).IsAssignableFrom(testCase.ExceptionType)).IsTrue();
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
            typeof(OcctException),
            typeof(OcctArgumentException),
            typeof(OcctArgumentOutOfRangeException),
            typeof(OcctArithmeticException),
            typeof(OcctInvalidOperationException),
            typeof(OcctNullObjectException),
            typeof(OcctOutOfMemoryException),
            typeof(OcctOverflowException),
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
        foreach (var leafType in exceptionTypes.Skip(1))
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
        var properties = typeof(IOcctException).GetProperties();

        await Assert.That(properties.Select(static property => property.Name))
            .IsEquivalentTo(["ErrorKind", "NativeTypeName", "NativeStackTrace",]);
        await Assert.That(properties.All(static property => property.CanRead && !property.CanWrite)).IsTrue();
        await Assert.That(properties.Single(static property => property.Name == "ErrorKind").PropertyType)
            .IsEqualTo(typeof(OcctErrorKind));
        await Assert.That(properties.Single(static property => property.Name == "NativeTypeName").PropertyType)
            .IsEqualTo(typeof(string));
        await Assert.That(properties.Single(static property => property.Name == "NativeStackTrace").PropertyType)
            .IsEqualTo(typeof(string));
    }
}