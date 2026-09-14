// -----------------------------------------------------------------------
// <copyright file="BindingEmissionProfile.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Supplies the finite library policy required by the provider-neutral paired emitters.
/// </summary>
public sealed class BindingEmissionProfile
{
    /// <summary>
    /// Gets the generated managed namespace.
    /// </summary>
    public required string CSharpNamespace { get; init; }

    /// <summary>
    /// Gets a value indicating whether generated record representations are internal.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// Gets the native interface name that identifies the intrusive-reference root, when present.
    /// </summary>
    public string IntrusiveRootInterfaceName { get; init; } = "";

    /// <summary>
    /// Gets the fully qualified managed intrusive-reference root interface.
    /// </summary>
    public string ManagedIntrusiveRootInterface { get; init; } = "";

    /// <summary>
    /// Gets the fully qualified open owning intrusive-handle type.
    /// </summary>
    public string ManagedOwningIntrusiveHandle { get; init; } = "";

    /// <summary>
    /// Gets the fully qualified open borrowed intrusive-handle type.
    /// </summary>
    public string ManagedBorrowedIntrusiveHandle { get; init; } = "";

    /// <summary>
    /// Gets the fully qualified managed native-error projection helper.
    /// </summary>
    public required string ManagedNativeErrorProjection { get; init; }

    /// <summary>
    /// Gets the export that clears one native error payload.
    /// </summary>
    public required string NativeErrorClearExport { get; init; }

    /// <summary>
    /// Gets native source text written before declaration-specific includes.
    /// </summary>
    public required string NativePreamble { get; init; }

    /// <summary>
    /// Gets provider headers already supplied by <see cref="NativePreamble"/>.
    /// </summary>
    public IReadOnlyList<string> NativePreambleHeaders { get; init; } = [];

    /// <summary>
    /// Gets native source text written when at least one operation can throw.
    /// </summary>
    public required string NativeErrorPreamble { get; init; }

    /// <summary>
    /// Gets the native error payload type.
    /// </summary>
    public string NativeErrorType { get; init; } = "NativeError";

    /// <summary>
    /// Gets the native helper used to populate an error payload.
    /// </summary>
    public string NativeErrorSetter { get; init; } = "NativeError_Set";

    /// <summary>
    /// Gets ordered typed native exception projections.
    /// </summary>
    public IReadOnlyList<BindingNativeExceptionProjection> NativeExceptionProjections { get; init; } = [];

    /// <summary>
    /// Gets the optional stack expression passed when projecting an unknown native exception.
    /// </summary>
    public string? UnknownNativeStackExpression { get; init; }

    /// <summary>
    /// Gets the expression that decrements an intrusive reference and reports whether deletion is required.
    /// </summary>
    public string IntrusiveReleaseCondition { get; init; } = "self->DecrementRefCounter() == 0";

    /// <summary>
    /// Gets the statement that deletes an intrusive object.
    /// </summary>
    public string IntrusiveDeleteStatement { get; init; } = "self->Delete();";

    /// <summary>
    /// Gets the statement that retains an intrusive result pointer.
    /// </summary>
    public string IntrusiveRetainStatement { get; init; } = "result->IncrementRefCounter();";

    /// <summary>
    /// Creates a detached snapshot for one semantic model.
    /// </summary>
    /// <returns>A profile whose collection values cannot observe caller mutations.</returns>
    internal BindingEmissionProfile Snapshot()
    {
        return new()
        {
            CSharpNamespace = CSharpNamespace,
            IsInternal = IsInternal,
            IntrusiveRootInterfaceName = IntrusiveRootInterfaceName,
            ManagedIntrusiveRootInterface = ManagedIntrusiveRootInterface,
            ManagedOwningIntrusiveHandle = ManagedOwningIntrusiveHandle,
            ManagedBorrowedIntrusiveHandle = ManagedBorrowedIntrusiveHandle,
            ManagedNativeErrorProjection = ManagedNativeErrorProjection,
            NativeErrorClearExport = NativeErrorClearExport,
            NativePreamble = NativePreamble,
            NativePreambleHeaders = Array.AsReadOnly(NativePreambleHeaders.ToArray()),
            NativeErrorPreamble = NativeErrorPreamble,
            NativeErrorType = NativeErrorType,
            NativeErrorSetter = NativeErrorSetter,
            NativeExceptionProjections = Array.AsReadOnly(NativeExceptionProjections.ToArray()),
            UnknownNativeStackExpression = UnknownNativeStackExpression,
            IntrusiveReleaseCondition = IntrusiveReleaseCondition,
            IntrusiveDeleteStatement = IntrusiveDeleteStatement,
            IntrusiveRetainStatement = IntrusiveRetainStatement,
        };
    }
}