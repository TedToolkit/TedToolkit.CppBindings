namespace TedToolkit.Occt.Generator.Models;

public sealed class FieldModel
{
    public long Offset { get; set; }
    public required string Name { get; init; }
    public required TypeModel Type { get; init; }
}