using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.Occt;

/// <summary>
/// <para>
/// Intrusive smart pointer for use with Standard_Transient class and its descendants.
/// </para>
/// <para>
/// This class is similar to boost::intrusive_ptr&lt;&gt;. The reference counter
/// is part of the base class (Standard_Transient), thus creation of a handle
/// does not require allocation of additional memory for the counter.
/// All handles to the same object share the common counter; object is deleted
/// when the last handle pointing on it is destroyed. It is safe to create a new
/// handle from plain C pointer to the object already pointed by another handle.
/// The same object can be referenced by handles of different types (as soon as
/// they are compatible with the object type).
/// </para>
/// <para>
/// Handle has type cast operator to const reference to handle to the base
/// types, which allows it to be passed by reference in functions accepting
/// reference to handle to base class, without copying.
/// </para>
/// <para>
/// By default, the type cast operator is provided also for non-const reference.
/// These casts (potentially unsafe) can be disabled by defining macro
/// OCCT_HANDLE_NOCAST; if it is defined, generalized copy constructor
/// and assignment operators are defined allowing to initialize handle
/// of base type from handle to derived type.
/// </para>
/// </summary>
/// <typeparam name="TElement">transient</typeparam>
// ReSharper disable once InconsistentNaming
public sealed unsafe class handle<TElement> : IDisposable
    where TElement : unmanaged, IDisposable
{
    private nint _handle;

    public ref TElement Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var currentAddress = Volatile.Read(ref _handle);
            if (currentAddress == IntPtr.Zero)
            {
                ThrowObjectDisposedException();
            }

            return ref *(TElement*)currentAddress;
        }
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(typeof(handle<TElement>).FullName);
    }

    public bool IsDisposed
    {
        get
        {
            return Volatile.Read(ref _handle) == IntPtr.Zero;
        }
    }

    public handle(TElement* handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _handle = (nint)handle;
    }

    ~handle()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }


    private void Release()
    {
        var previousAddress = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (previousAddress == IntPtr.Zero)
        {
            return;
        }

        ((TElement*)previousAddress)->Dispose();
    }
}