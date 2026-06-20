using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
[StructLayout(LayoutKind.Sequential)]
#pragma warning disable IDE1006
public readonly unsafe ref struct handle<TElement> :
#pragma warning restore IDE1006
    IHandle<TElement>
    where TElement : unmanaged, IStandard_Transient
{
    private readonly nint _handle;

    internal handle(TElement* handle)
    {
        _handle = (nint)handle;
    }

    public ref TElement Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return ref *NativeHandle; }
    }

    public TElement* NativeHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return (TElement*)_handle; }
    }
}