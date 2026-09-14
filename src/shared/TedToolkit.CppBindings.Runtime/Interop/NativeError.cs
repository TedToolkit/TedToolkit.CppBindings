// -----------------------------------------------------------------------
// <copyright file="NativeError.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents the generated-only sequential carrier returned by the native binding boundary.
/// </summary>
/// <remarks>
/// Public visibility allows independently generated wrapper assemblies to name the transport.
/// Its fields directly reproduce the generated native boundary layout. Handwritten callers should
/// use generated C++ operations instead.
/// </remarks>
[GeneratedCodeOnly]
[SuppressMessage(
    "Design",
    "CA1051:Do not declare visible instance fields",
    Justification = "The public fields are the generated-only sequential ABI transport.")]
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "This generated-only transport is an owning ABI slot, not a comparable value.")]
[StructLayout(LayoutKind.Sequential)]
public struct NativeError
{
    /// <summary>
    /// Stores the native error category.
    /// </summary>
    public int Kind;

    /// <summary>
    /// Stores the native-owned null-terminated UTF-8 type-name pointer.
    /// </summary>
    public nint TypeName;

    /// <summary>
    /// Stores the native-owned null-terminated UTF-8 message pointer.
    /// </summary>
    public nint Message;

    /// <summary>
    /// Stores the native-owned null-terminated UTF-8 stack-text pointer.
    /// </summary>
    public nint StackTrace;
}