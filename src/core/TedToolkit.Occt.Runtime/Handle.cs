// -----------------------------------------------------------------------
// <copyright file="Handle.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.Occt;

/// <summary>
/// Represents the managed owner of one intrusive native reference to an OCCT
/// <c>Standard_Transient</c> object.
/// </summary>
/// <typeparam name="T">The exact-layout unmanaged transient projection.</typeparam>
/// <remarks>
/// Assigning this reference type aliases the same owner and disposal state. <see cref="Value"/>
/// provides non-owning access only while the Handle is undisposed; callers must not overlap that
/// access with disposal. The native module containing the release function must remain loaded until
/// this owner is disposed or finalized.
/// </remarks>
public sealed unsafe class Handle<T> : IDisposable
    where T : unmanaged, IStandard_Transient
{
    private readonly delegate* unmanaged[Cdecl]<T*, void> _release;

    private nint _value;

    /// <summary>
    /// Initializes a new Handle by adopting one already-owned intrusive native reference.
    /// </summary>
    /// <param name="value">The non-null native object address to adopt.</param>
    /// <param name="release">
    /// The non-null, non-throwing <c>cdecl void(T*)</c> function from the native module that produced
    /// <paramref name="value"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> or <paramref name="release"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The constructor does not retain <paramref name="value"/>. Ownership transfers to this Handle
    /// only after both arguments have been validated.
    /// </remarks>
    public Handle(T* value, delegate* unmanaged[Cdecl]<T*, void> release)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(release);

        _value = (nint)value;
        _release = release;
    }

    /// <summary>
    /// Gets a non-owning managed reference to the live native object.
    /// </summary>
    /// <value>The exact-layout value at the adopted native address.</value>
    /// <exception cref="ObjectDisposedException">This Handle has already been disposed.</exception>
    /// <remarks>
    /// Access does not extend native lifetime. The returned reference must not be retained or used
    /// concurrently with <see cref="Dispose"/>.
    /// </remarks>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var value = Volatile.Read(ref _value);
            if (value == 0)
            {
                ThrowObjectDisposedException();
            }

            return ref *(T*)value;
        }
    }

    /// <summary>
    /// Releases the adopted intrusive native reference at most once.
    /// </summary>
    /// <remarks>
    /// Repeated calls are no-ops. The configured native release function must not throw across the
    /// unmanaged boundary.
    /// </remarks>
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Handle{T}"/> class.
    /// </summary>
    ~Handle()
    {
        Release();
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(typeof(Handle<T>).FullName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Release()
    {
        var value = Interlocked.Exchange(ref _value, 0);
        if (value == 0)
        {
            return;
        }

        _release((T*)value);
    }
}