namespace TedToolkit.Occt.Attributes;

[AttributeUsage(AttributeTargets.Struct
                | AttributeTargets.Interface
                | AttributeTargets.Parameter
                | AttributeTargets.ReturnValue
                | AttributeTargets.Field)]
public sealed class NativeTypeNameAttribute(string nativeTypeName) : Attribute
{
    public string NativeTypeName { get; } = nativeTypeName;
}