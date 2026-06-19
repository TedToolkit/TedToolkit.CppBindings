using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public class MethodModel
{
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }
    public required IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; }

    public required bool NoExceptions { get; set; }
    public required bool IsConst { get; set; }
    public required TypeModel ReturnType { get; init; }
    public required string MethodName { get; init; }
    public required IReadOnlyList<ParameterModel> Parameters { get; init; }
}