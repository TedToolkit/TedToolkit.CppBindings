using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public sealed class EnumMemberModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }

    public required string Name { get; init; }

    public required IExpression Value { get; init; }
}
