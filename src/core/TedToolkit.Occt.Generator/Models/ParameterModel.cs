using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public class ParameterModel
{
    public required IReadOnlyList<IDescriptionItem> DescriptionItems { get; init; }

    public required TypeModel Type { get; init; }
    public required string Name { get; init; }
}