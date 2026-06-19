using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TedToolkit.Occt;


/// <inheritdoc cref="handle{TElement}"/>
public sealed unsafe class Handle<TElement> :
    IHandle<TElement>,
    IDisposable
    where TElement : unmanaged, IHandleElement
{
    private nint _handle;

    public ref TElement Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return ref *NativeHandle;
        }
    }

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

    public bool IsDisposed
    {
        get { return Volatile.Read(ref _handle) == IntPtr.Zero; }
    }

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

    ~Handle()
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

        ((TElement*)previousAddress)->Delete();
    }

    public static implicit operator handle<TElement>(Handle<TElement> handle)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(handle);
#else
        if (handle is null)
        {
            throw new ArgumentNullException(nameof(handle));
        }
#endif
        return new(handle.NativeHandle);
    }
}