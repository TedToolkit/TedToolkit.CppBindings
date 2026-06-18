using System.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators;

namespace TedToolkit.Occt.Generator.Generators;

public sealed class EnumGenerator(EnumModel enumModel) : IGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("namespace TedToolkit.Occt;");
        builder.AppendLine();
        builder.Append("public enum ");
        builder.Append(enumModel.Name);
        builder.Append(" : ");
        builder.Append(enumModel.UnderlyingType.ToCode());
        builder.AppendLine();
        builder.AppendLine("{");

        foreach (var member in enumModel.Members)
        {
            builder.Append("    ");
            builder.Append(member.Name);
            builder.Append(" = ");
            builder.Append(member.Value);
            builder.AppendLine(",");
        }

        builder.AppendLine("}");
        return Task.FromResult(builder.ToString());
    }
}
