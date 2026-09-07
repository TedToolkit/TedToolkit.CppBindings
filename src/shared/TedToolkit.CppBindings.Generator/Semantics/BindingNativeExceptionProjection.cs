// -----------------------------------------------------------------------
// <copyright file="BindingNativeExceptionProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes one provider-specific native exception mapping used by the shared wrapper emitter.
/// </summary>
/// <param name="CppType">The caught C++ exception type.</param>
/// <param name="Code">The stable provider error code.</param>
/// <param name="NativeTypeExpression">The expression naming the native exception type.</param>
/// <param name="MessageExpression">The expression yielding an optional UTF-8 message.</param>
public sealed record BindingNativeExceptionProjection(
    string CppType,
    int Code,
    string NativeTypeExpression,
    string MessageExpression);