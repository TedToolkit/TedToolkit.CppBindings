using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public sealed class FieldModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }
    public long Offset { get; set; }
    public required string Name { get; init; }
    public required TypeModel Type { get; init; }
}