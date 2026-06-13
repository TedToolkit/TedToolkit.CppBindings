namespace TedToolkit.Occt.Generator.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public sealed class OcctHeaderFilePathAttribute(string filePath) : Attribute
{
    public string FilePath { get; } = filePath;
}