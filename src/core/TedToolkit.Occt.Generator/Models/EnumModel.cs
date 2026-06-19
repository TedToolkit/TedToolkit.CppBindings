using TedToolkit.RoslynHelper.Generators;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Models;

public sealed class EnumModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    public required string Name { get; init; }

    public required string SourceType { get; init; }

    public required DataType UnderlyingType { get; init; }

    public required IReadOnlyList<EnumMemberModel> Members { get; init; }
}
