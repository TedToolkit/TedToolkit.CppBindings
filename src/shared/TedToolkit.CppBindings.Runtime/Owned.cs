// -----------------------------------------------------------------------
// <copyright file="Owned.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.CppBindings;

/// <summary>
/// Represents the managed owner of one C++ object returned with value ownership.
/// </summary>
/// <typeparam name="T">The exact-layout unmanaged RAII projection contained by this owner.</typeparam>
/// <remarks>
/// The native object is placement-constructed directly in this owner's managed <see cref="Value"/>
/// field by generated code. Assigning this reference type aliases the same owner and disposal state.
/// <see cref="Value"/> is non-owning and must not be used after or concurrently with disposal.
/// </remarks>
public sealed unsafe class Owned<T> : IDisposable, ICppOwner<T>
    where T : unmanaged, ICppRaii
{
    private readonly nint _destroy;

    private T _value;

    private int _disposed;

    /// <summary>
    /// Initializes storage for a generated wrapper to placement-construct one native RAII object.
    /// </summary>
    /// <param name="destroy">
    /// The non-null, non-throwing <c>cdecl void(T*)</c> destructor from the native module that will
    /// construct the contained object.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="destroy"/> is null.</exception>
    /// <remarks>
    /// This constructor reserves the contained field but does not construct <typeparamref name="T"/>.
    /// Generated bindings must reject native types whose alignment cannot be represented safely by
    /// this direct managed-field storage model.
    /// Generated code must suppress finalization if native placement construction does not succeed.
    /// </remarks>
    [GeneratedCodeOnly]
    public Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)
    {
        ArgumentNullException.ThrowIfNull(destroy);
        _destroy = (nint)destroy;
    }

    /// <summary>
    /// Gets a non-owning managed reference to the contained native object.
    /// </summary>
    /// <value>The exact-layout value contained directly in this owner.</value>
    /// <exception cref="ObjectDisposedException">This owner has already been disposed.</exception>
    /// <remarks>
    /// Access does not extend lifetime. The returned reference must not be retained or used
    /// concurrently with <see cref="Dispose"/>. Generated native calls stabilize it with
    /// <see langword="fixed"/> and keep this owner alive through the call.
    /// </remarks>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                ThrowObjectDisposedException();
            }

            return ref _value;
        }
    }

    /// <summary>
    /// Invokes the configured native destructor for the contained object at most once.
    /// </summary>
    /// <remarks>
    /// Repeated calls are no-ops. This operation neither invokes intrusive release nor frees the
    /// managed storage containing the object. The configured native destructor must not throw across
    /// the unmanaged boundary.
    /// </remarks>
    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Owned{T}"/> class.
    /// </summary>
    ~Owned()
    {
        Destroy();
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(typeof(Owned<T>).FullName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Destroy()
    {
        if (_destroy == 0 || Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        fixed (T* value = &_value)
        {
            NativeCleanup.Invoke(_destroy, (nint)value);
        }
    }
}