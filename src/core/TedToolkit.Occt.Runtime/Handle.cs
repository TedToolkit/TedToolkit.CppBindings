// -----------------------------------------------------------------------
// <copyright file="Handle.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Owns a native OCCT handle and releases it when disposed.
/// </summary>
/// <typeparam name="TElement">The unmanaged transient element type.</typeparam>
public sealed unsafe class Handle<TElement> :
    IHandle<TElement>,
    IDisposable
    where TElement : unmanaged, IStandard_Transient
{
    private nint _handle;

    /// <summary>
    /// Gets a managed reference to the native element.
    /// </summary>
    public ref TElement Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref *NativeHandle;
        }
    }

    /// <summary>
    /// Gets the native pointer for the underlying element.
    /// </summary>
    public TElement* NativeHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var currentAddress = Volatile.Read(ref _handle);
            if (currentAddress == IntPtr.Zero)
            {
                ThrowObjectDisposedException();
            }

            return (TElement*)currentAddress;
        }
    }

#if NET6_0_OR_GREATER
    [DoesNotReturn]
#endif
    private static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(typeof(Handle<TElement>).FullName);
    }

    /// <summary>
    /// Gets a value indicating whether the native handle has been released.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            return Volatile.Read(ref _handle) == IntPtr.Zero;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Handle{TElement}"/> class.
    /// </summary>
    /// <param name="handle">The native handle to own.</param>
    internal Handle(TElement* handle)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(handle);
#else
        if (handle is null)
        {
            throw new ArgumentNullException(nameof(handle));
        }
#endif
        _handle = (nint)handle;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Handle{TElement}"/> class.
    /// </summary>
    ~Handle()
    {
        Release();
    }

    /// <summary>
    /// Releases the native handle.
    /// </summary>
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a lightweight handle view for this owned handle.
    /// </summary>
    /// <returns>A lightweight handle view over the same native element.</returns>
    public handle<TElement> Tohandle()
    {
        return new(NativeHandle);
    }

    /// <summary>
    /// Converts the owned handle into a lightweight handle view.
    /// </summary>
    /// <param name="handle">The owned handle to convert.</param>
    /// <returns>A lightweight handle view over the same native element.</returns>
    public static implicit operator handle<TElement>(Handle<TElement> handle)
    {
        if (handle is null)
        {
            return default;
        }

        return handle.Tohandle();
    }

    private void Release()
    {
        var previousAddress = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (previousAddress == IntPtr.Zero)
        {
            return;
        }

        ((TElement*)previousAddress)->Delete();
    }
}