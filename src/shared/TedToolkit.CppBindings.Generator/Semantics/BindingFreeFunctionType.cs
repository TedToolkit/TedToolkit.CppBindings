// -----------------------------------------------------------------------
// <copyright file="BindingFreeFunctionType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator.Semantics;

/// <summary>
/// Describes the native and managed projections of a free-function type.
/// </summary>
/// <param name="NativeType">The C++ type or, for an lvalue reference, its referred-to type.</param>
/// <param name="ManagedType">The C# type or, for an lvalue reference, its referred-to type.</param>
/// <param name="IsReference">Whether the C++ type is an lvalue reference.</param>
/// <param name="IsReadOnly">Whether the lvalue reference is const-qualified.</param>
public sealed record BindingFreeFunctionType(
    string NativeType,
    string ManagedType,
    bool IsReference = false,
    bool IsReadOnly = false)
{
    /// <summary>
    /// Gets whether the source takes or returns a proved record by value.
    /// </summary>
    internal bool IsRecordValue { get; init; }

    /// <summary>
    /// Gets whether the native argument uses caller storage.
    /// </summary>
    internal bool IsPassedByAddress
    {
        get
        {
            return IsReference || IsRecordValue;
        }
    }

    /// <summary>
    /// Gets the managed ABI type.
    /// </summary>
    internal string ManagedTransportType
    {
        get
        {
            return IsPassedByAddress ? ManagedType + "*" : ManagedType;
        }
    }

    /// <summary>
    /// Gets the native adapter ABI type.
    /// </summary>
    internal string NativeTransportType
    {
        get
        {
            var type = IsRecordValue ? "const " + NativeType : NativeType;
            return IsPassedByAddress ? type + "*" : type;
        }
    }
}