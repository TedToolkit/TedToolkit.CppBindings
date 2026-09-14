#if ANALYZER_ONLY
public sealed class Consumer;
#else
using TedToolkit.CppBindings;

#if OCCT_HOOK || OCCT_LIFETIME || BORROWED
using TedToolkit.CppBindings.Occt;
#endif

public struct NativeValue : ICppRaii
{
    public int Number;
}

public static unsafe class Consumer
{
#if GENERIC_HOOK || SUPPRESSED
#if SUPPRESSED
#pragma warning disable TTCB001, TTCB002
#endif
    public static Owned<NativeValue> Create() => new(null);
#endif
#if GENERIC_LIFETIME || SUPPRESSED
    public static ref NativeValue Escape(Owned<NativeValue> owner) => ref owner.Value;
#endif
#if VALID
    public static int Read(Owned<NativeValue> owner)
    {
        ref var value = ref owner.Value;
        var number = value.Number;
        GC.KeepAlive(owner);
        return number;
    }
#endif
#if CUSTOM_OWNER
    public static ref int Escape(OtherLibraryOwner owner) => ref owner.Value;
#endif
#if OCCT_HOOK
    public static Handle<Transient> Create() => new(null, null);
#endif
#if OCCT_LIFETIME
    public static int ReadDisposed(Handle<Transient> owner)
    {
        owner.Dispose();
        return owner.Value.Number;
    }
#endif
#if BORROWED
    public static ref int Borrow(in handle<int> value) => ref value.Value;
#endif
}

#if CUSTOM_OWNER
public sealed class OtherLibraryOwner : ICppOwner<int>
{
    private int value;
    public ref int Value => ref value;
}
#endif
#if OCCT_HOOK || OCCT_LIFETIME
public struct Transient : IStandard_Transient
{
    public int Number;
}
#endif
#endif
