namespace TedToolkit.Occt;

public interface IHandle<TElement>
    where TElement : unmanaged, IHandleElement
{
    ref TElement Value { get; }
    unsafe TElement* NativeHandle { get; }
}