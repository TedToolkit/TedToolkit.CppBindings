namespace TedToolkit.Occt;

public interface IHandle<TElement>
    where TElement : unmanaged, IStandard_Transient
{
    ref TElement Value { get; }
    unsafe TElement* NativeHandle { get; }
}