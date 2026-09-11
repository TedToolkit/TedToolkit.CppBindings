// -----------------------------------------------------------------------
// <copyright file="PublicSurfaceTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Occt;

namespace TedToolkit.CppBindings.Runtime.Tests.AssemblyInfoTests;

/// <summary>
/// Verifies the independently consumable generic and provider Runtime assembly boundaries.
/// </summary>
internal sealed class PublicSurfaceTests
{
    /// <summary>
    /// Verifies that the generic package exports only the approved provider-neutral contracts.
    /// </summary>
    /// <returns>A task that completes when the assembly assertions finish.</returns>
    [Test]
    public async Task Should_expose_only_generic_runtime_contracts_Async()
    {
        var assembly = typeof(Owned<>).Assembly;
        await Assert.That(assembly.GetName().Name).IsEqualTo("TedToolkit.CppBindings.Runtime");
        await Assert.That(assembly.GetExportedTypes().Select(static type => type.FullName!))
            .IsEquivalentTo([
                "TedToolkit.CppBindings.Owned`1",
                "TedToolkit.CppBindings.ICppRaii",
                "TedToolkit.CppBindings.ICppOwner`1",
                "TedToolkit.CppBindings.NativeTypeNameAttribute",
                "TedToolkit.CppBindings.GeneratedCodeOnlyAttribute",
                "TedToolkit.CppBindings.NativeError",
                "TedToolkit.CppBindings.INativeException",
                "TedToolkit.CppBindings.NativeException",
                "TedToolkit.CppBindings.NativeArgumentException",
                "TedToolkit.CppBindings.NativeArgumentOutOfRangeException",
                "TedToolkit.CppBindings.NativeArithmeticException",
                "TedToolkit.CppBindings.NativeInvalidOperationException",
                "TedToolkit.CppBindings.NativeNullObjectException",
                "TedToolkit.CppBindings.NativeOutOfMemoryException",
                "TedToolkit.CppBindings.NativeOverflowException",
                "TedToolkit.CppBindings.NativeStandardException",
                "TedToolkit.CppBindings.NativeUnknownException",
                "TedToolkit.CppBindings.NativeErrorExtension",
                "TedToolkit.CppBindings.NativeErrorProjection",
            ]);
        await Assert.That(assembly.GetReferencedAssemblies().Any(static dependency =>
            dependency.Name?.StartsWith("TedToolkit", StringComparison.Ordinal) == true
            || dependency.Name?.StartsWith("Clang", StringComparison.Ordinal) == true)).IsFalse();
    }

    /// <summary>
    /// Verifies that OCCT ownership and errors stay in the provider and depend inward on Runtime.
    /// </summary>
    /// <returns>A task that completes when the provider assertions finish.</returns>
    [Test]
    public async Task Should_keep_occt_contracts_in_the_provider_Async()
    {
        var assembly = typeof(Handle<>).Assembly;
        await Assert.That(assembly.GetName().Name).IsEqualTo("TedToolkit.CppBindings.Occt.Runtime");
        await Assert.That(assembly).IsNotEqualTo(typeof(Owned<>).Assembly);
        await Assert.That(assembly.GetExportedTypes().Select(static type => type.FullName!))
            .IsEquivalentTo([
                "TedToolkit.CppBindings.Occt.Handle`1",
                "TedToolkit.CppBindings.Occt.handle`1",
                "TedToolkit.CppBindings.Occt.IStandard_Transient",
                "TedToolkit.CppBindings.Occt.NativeErrorProjection",
                "TedToolkit.CppBindings.Occt.IOcctException",
                "TedToolkit.CppBindings.Occt.OcctException",
                "TedToolkit.CppBindings.Occt.OcctFailureException",
            ]);
        await Assert.That(assembly.GetReferencedAssemblies().Select(static dependency => dependency.Name))
            .Contains("TedToolkit.CppBindings.Runtime");
        await Assert.That(typeof(handle<int>).GetInterfaces()).IsEmpty();
    }
}