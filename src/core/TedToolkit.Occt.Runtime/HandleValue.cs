// -----------------------------------------------------------------------
// <copyright file="HandleValue.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Represents the exact-layout, non-owning view of an OCCT <c>opencascade::handle&lt;T&gt;</c> value.
/// </summary>
/// <typeparam name="T">The pointed-to unmanaged native record.</typeparam>
/// <remarks>
/// This value neither retains nor releases its target. Any reference obtained through
/// <see cref="Value"/> remains subject to the original native handle owner's lifetime and
/// invalidation rules.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "This type is a native layout view rather than a comparable managed value.")]
#pragma warning disable SA1300, SA1649
public readonly unsafe struct handle<T>
#pragma warning restore SA1300, SA1649
    where T : unmanaged
{
    private readonly T* _value;

    /// <summary>
    /// Gets a non-owning reference to the pointed-to native object.
    /// </summary>
    /// <value>The native object referenced by this handle value.</value>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref *_value;
        }
    }
}