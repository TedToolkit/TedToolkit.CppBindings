using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public class MethodModel
{
    public bool IsReturnVoid => ReturnType.CppTypeName is "void";
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }
    public required IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; }

    public required bool NoExceptions { get; set; }
    public required bool IsConst { get; set; }
    public required TypeModel ReturnType { get; init; }
    public required string MethodName { get; init; }
    public required IReadOnlyList<ParameterModel> Parameters { get; init; }

    public string GetMethodInteropName(RecordModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        using var builder = ZString.CreateStringBuilder();
        builder.Append(model.Type.CppTypeName);
        builder.Append("_");
        builder.Append(MethodName);
        foreach (var parameterModel in Parameters)
        {
            builder.Append('_');
            builder.Append(parameterModel.Type.CppTypeName.ToValidCSharpName());
        }

        return builder.ToString();
    }
}