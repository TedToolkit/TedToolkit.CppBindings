namespace TedToolkit.Occt.Generator.Models;

public class MethodModel
{
    public required TypeModel ReturnType { get; init; }
    public required string MethodName { get; init; }
    public required IReadOnlyList<ParameterModel> Parameters { get; init; }
}