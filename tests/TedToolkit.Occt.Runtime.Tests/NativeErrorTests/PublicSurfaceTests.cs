// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using TedToolkit.Occt;
using TedToolkit.Occt.Runtime;

namespace TedToolkit.Occt.Runtime.Tests.NativeErrorTests;

/// <summary>
/// Verifies the public generated-only native-error transport contract.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that generated wrappers receive the direct ABI fields without managed behavior.
    /// </summary>
    /// <returns>A task that completes when the carrier assertions finish.</returns>
    [Test]
    public async Task Should_expose_only_the_generated_native_error_carrier_Async()
    {
        var carrierType = typeof(NativeError);

        await Assert.That(carrierType.IsPublic).IsTrue();
        await Assert.That(carrierType.IsValueType).IsTrue();
        await Assert.That(carrierType.GetCustomAttribute<GeneratedCodeOnlyAttribute>()).IsNotNull();
        var fields = carrierType.GetFields(BindingFlags.Instance | BindingFlags.Public);

        await Assert.That(carrierType.GetConstructors()).IsEmpty();
        await Assert.That(fields).Count().IsEqualTo(4);
        await Assert.That(fields.Select(static field => field.Name))
            .IsEquivalentTo(["Kind", "TypeName", "Message", "StackTrace",]);
        await Assert.That(fields.Single(static field => field.Name == "Kind").FieldType)
            .IsEqualTo(typeof(int));
        foreach (var field in fields.Where(static field => field.Name != "Kind"))
        {
            await Assert.That(field.FieldType).IsEqualTo(typeof(nint));
        }

        await Assert.That(carrierType.GetProperties(BindingFlags.Instance | BindingFlags.Public)).IsEmpty();
        await Assert.That(carrierType.GetMethods(
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)).IsEmpty();
    }

    /// <summary>
    /// Verifies that generated wrappers receive one public function-pointer projection entry point.
    /// </summary>
    /// <returns>A task that completes when the projection assertions finish.</returns>
    [Test]
    public async Task Should_expose_the_generated_projection_function_pointer_contract_Async()
    {
        var projectionType = typeof(NativeErrorProjection);
        var method = projectionType.GetMethod(
            "ThrowIfFailed",
            BindingFlags.Public | BindingFlags.Static);

        await Assert.That(projectionType.IsPublic).IsTrue();
        await Assert.That(projectionType.IsAbstract && projectionType.IsSealed).IsTrue();
        await Assert.That(projectionType.GetCustomAttribute<GeneratedCodeOnlyAttribute>()).IsNotNull();
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.GetCustomAttribute<GeneratedCodeOnlyAttribute>()).IsNotNull();

        var parameters = method.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(NativeError).MakeByRefType());
        await Assert.That(parameters[1].ParameterType.IsFunctionPointer).IsTrue();
        await Assert.That(parameters[1].ParameterType.GetFunctionPointerParameterTypes())
            .IsEquivalentTo([typeof(NativeError).MakePointerType(),]);
        await Assert.That(parameters[1].ParameterType.GetFunctionPointerReturnType()).IsEqualTo(typeof(void));
        await Assert.That(typeof(NativeError).Assembly.GetType("TedToolkit.Occt.NativeErrorClear")).IsNull();
    }
}