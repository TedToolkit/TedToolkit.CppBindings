using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public sealed class FieldModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }
    public required long Offset { get; init; }
    public required string Name { get; init; }
    public required TypeModel Type { get; init; }
}