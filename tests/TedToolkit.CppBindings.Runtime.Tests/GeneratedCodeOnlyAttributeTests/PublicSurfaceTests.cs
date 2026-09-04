// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.GeneratedCodeOnlyAttributeTests;

/// <summary>
/// Verifies the public generated-only marker contract.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies the marker shape and its first Runtime application.
    /// </summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task Should_expose_the_approved_marker_contract_Async()
    {
        var markerType = typeof(GeneratedCodeOnlyAttribute);
        var usage = markerType.GetCustomAttribute<AttributeUsageAttribute>();
        var constructor = typeof(Handle<TestTransient>).GetConstructors().Single();

        await Assert.That(markerType.IsPublic).IsTrue();
        await Assert.That(markerType.IsSealed).IsTrue();
        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(
            AttributeTargets.Constructor
            | AttributeTargets.Class
            | AttributeTargets.Struct
            | AttributeTargets.Method
            | AttributeTargets.Property
            | AttributeTargets.Event);
        await Assert.That(usage.AllowMultiple).IsFalse();
        await Assert.That(usage.Inherited).IsFalse();
        await Assert.That(constructor.GetCustomAttribute<GeneratedCodeOnlyAttribute>()).IsNotNull();
    }

    private struct TestTransient : IStandard_Transient;
}