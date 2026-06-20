using Cysharp.Text;

using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Models;

public enum MethodModelType
{
    Normal,
    New,
    Delete,
    Operator,
    Implicit,
    Explicit,
}

public class MethodModel
{
    public bool IsReturnVoid => ReturnType.CppTypeName is "void";
    public required IReadOnlyList<IRootDescriptionItem> DescriptionItems { get; init; }
    public required IReadOnlyList<IDescriptionItem> ReturnTypeDescriptionItems { get; init; }

    public required bool NoExceptions { get; init; }
    public required bool IsConst { get; init; }
    public required TypeModel ReturnType { get; init; }
    public required string MethodName { get; init; }
    public required MethodModelType Type { get; init; }
    public required IReadOnlyList<ParameterModel> Parameters { get; init; }

    public string GetInvokeName()
    {
        if (Type is not (MethodModelType.Operator or MethodModelType.Implicit or MethodModelType.Explicit))
        {
            return MethodName;
        }

        using var builder = ZString.CreateStringBuilder();
        var upperNext = false;

        foreach (var c in MethodName)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
                continue;
            }

            upperNext = true;
            builder.Append(c switch
            {
                ' ' => "",
                '=' => "Equal",
                '+' => "Plus",
                '-' => "Minus",
                '*' => "Star",
                '/' => "Slash",
                '%' => "Percent",
                '!' => "Bang",
                '<' => "Less",
                '>' => "Greater",
                '&' => "Ampersand",
                '|' => "Pipe",
                '^' => "Caret",
                '~' => "Tilde",
                '(' => "LeftParen",
                ')' => "RightParen",
                '[' => "LeftBracket",
                ']' => "RightBracket",
                ',' => "Comma",
                _ => "Char",
            });
        }

        return builder.ToString();
    }

    public string GetMethodInteropName(RecordModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        using var builder = ZString.CreateStringBuilder();
        builder.Append(model.Type.CppTypeName);
        builder.Append("_");
        builder.Append(GetInvokeName());
        foreach (var parameterModel in Parameters)
        {
            builder.Append('_');
            builder.Append(parameterModel.Type.CppTypeName.ToValidCSharpName());
        }

        return builder.ToString();
    }
}
