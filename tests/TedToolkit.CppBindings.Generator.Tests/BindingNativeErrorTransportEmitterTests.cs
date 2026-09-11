// -----------------------------------------------------------------------
// <copyright file="BindingNativeErrorTransportEmitterTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Generator.Semantics;

namespace TedToolkit.CppBindings.Generator.Tests;

/// <summary>
/// Verifies the single Shared native diagnostic transport emitter.
/// </summary>
internal sealed class BindingNativeErrorTransportEmitterTests
{
    /// <summary>
    /// Verifies standalone emission owns the complete carrier and exact-once clear mechanism.
    /// </summary>
    /// <returns>A task that completes when the generated source assertions finish.</returns>
    [Test]
    public async Task Should_emit_a_complete_standalone_transport_Async()
    {
        var header = BindingNativeErrorTransportEmitter.RenderHeader("NativeError_Clear", "NativeError_Set");
        var source = BindingNativeErrorTransportEmitter.RenderSource(
            "NativeError.h",
            "NativeError_Clear",
            "NativeError_Set",
            "NativeError_Copy");

        await Assert.That(header).Contains("struct NativeError");
        await Assert.That(header).Contains("char* StackTrace;");
        await Assert.That(header).Contains("extern \"C\" void NativeError_Clear");
        await Assert.That(source).Contains("std::free(error->TypeName);");
        await Assert.That(source).Contains("error->StackTrace = nullptr;");
        await Assert.That(source).Contains("*error = {};");
    }

    /// <summary>
    /// Verifies optional provider stack inputs remain facts rather than another transport implementation.
    /// </summary>
    /// <returns>A task that completes when the stack-aware assertions finish.</returns>
    [Test]
    public async Task Should_compose_a_provider_stack_helper_without_owning_it_Async()
    {
        const string preamble = "#include <provider/errors.h>\ninline const char* Stack() { return nullptr; }";
        var header = BindingNativeErrorTransportEmitter.RenderHeader(
            "Provider_Clear",
            "Provider_Set",
            includeStackTrace: true,
            providerPreamble: preamble);
        var source = BindingNativeErrorTransportEmitter.RenderSource(
            "ProviderError.hpp",
            "Provider_Clear",
            "Provider_Set",
            "ProviderCopy",
            includeStackTrace: true);

        await Assert.That(header).Contains(preamble);
        await Assert.That(header).Contains("const char* stackTrace");
        await Assert.That(source).Contains("error->StackTrace = ProviderCopy(stackTrace);");
    }

    /// <summary>
    /// Verifies finite profiles consume the same carrier and ownership implementation inline.
    /// </summary>
    /// <returns>A task that completes when inline assertions finish.</returns>
    [Test]
    public async Task Should_emit_the_same_transport_inline_Async()
    {
        var source = BindingNativeErrorTransportEmitter.RenderInline("Finite_Clear", "SetError", "CopyText");

        await Assert.That(source).Contains("struct NativeError");
        await Assert.That(source).Contains("static void SetError");
        await Assert.That(source).Contains("extern \"C\" void Finite_Clear");
        await Assert.That(source).Contains("error->StackTrace = nullptr;");
    }
}