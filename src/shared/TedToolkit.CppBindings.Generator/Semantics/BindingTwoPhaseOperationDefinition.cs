// -----------------------------------------------------------------------
// <copyright file="BindingTwoPhaseOperationDefinition.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes an owner operation that obtains counts and then copies native buffers.
/// </summary>
/// <param name="ContainingType">The managed static containing type.</param>
/// <param name="MethodName">The managed method name.</param>
/// <param name="ResultName">The composite result containing the copied arrays.</param>
/// <param name="Owner">The owner receiver.</param>
/// <param name="Buffers">The ordered counted buffers.</param>
/// <param name="OverflowMessage">The managed array-length overflow message.</param>
/// <param name="CountExport">The native count-phase export.</param>
/// <param name="CountNativeBody">The provider-owned count-phase algorithm body.</param>
/// <param name="NativeExport">The native copy-phase export.</param>
/// <param name="NativeBody">The provider-owned copy-phase algorithm body.</param>
public sealed record BindingTwoPhaseOperationDefinition(
    string ContainingType,
    string MethodName,
    string ResultName,
    BindingOwnerParameterDefinition Owner,
    IReadOnlyList<BindingTwoPhaseBufferDefinition> Buffers,
    string OverflowMessage,
    string CountExport,
    string CountNativeBody,
    string NativeExport,
    string NativeBody) : BindingFiniteOperationDefinition(NativeExport, NativeBody);