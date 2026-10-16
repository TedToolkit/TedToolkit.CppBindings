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
/// Generated code placement-constructs the native object in managed storage. Projections implementing
/// <see cref="ICppStableAddress"/> use a pinned array element; other projections use a direct field.
/// Assigning this reference type aliases the same owner and disposal state.
/// <see cref="Value"/> is non-owning and must not be used after or concurrently with disposal.
/// </remarks>
public sealed unsafe class Owned<T> : IDisposable, ICppOwner<T>
    where T : unmanaged, ICppRaii
{
    private static readonly bool RequiresStableAddress = typeof(ICppStableAddress).IsAssignableFrom(typeof(T));

    private readonly nint _destroy;

    private readonly T[]? _stableValue;

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
    /// This constructor reserves storage but does not construct <typeparamref name="T"/>.
    /// Generated bindings must reject native types whose alignment cannot be represented safely by
    /// their managed field or pinned array element.
    /// Generated code must suppress finalization if native placement construction does not succeed.
    /// </remarks>
    [GeneratedCodeOnly]
    public Owned(delegate* unmanaged[Cdecl]<T*, void> destroy)
    {
        ArgumentNullException.ThrowIfNull(destroy);
        _stableValue = RequiresStableAddress ? GC.AllocateArray<T>(1, pinned: true) : null;
        _destroy = (nint)destroy;
    }

    /// <summary>
    /// Gets a non-owning managed reference to the contained native object.
    /// </summary>
    /// <value>The exact-layout value in the storage reserved by this owner.</value>
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

            return ref Storage;
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

    private ref T Storage
    {
        get
        {
            return ref (_stableValue is null ? ref _value : ref _stableValue[0]);
        }
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

        fixed (T* value = &Storage)
        {
            NativeCleanup.Invoke(_destroy, (nint)value);
        }
    }
}