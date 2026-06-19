using System.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.Occt.Generator.Generators.EnumGenerator>;

namespace TedToolkit.Occt.Generator.Generators;

public sealed class EnumGenerator(EnumModel enumModel) : IGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var enumDeclaration = Enum(enumModel.Name, enumModel.UnderlyingType).Public;

        foreach (var enumModelDescriptionItem in enumModel.DescriptionItems)
        {
            enumDeclaration.AddRootDescription(enumModelDescriptionItem);
        }

        foreach (var member in enumModel.Members)
        {
            var enumMember = EnumMember(member.Name, member.Value);
            foreach (var enumModelDescriptionItem in member.DescriptionItems)
            {
                enumMember.AddRootDescription(enumModelDescriptionItem);
            }

            enumDeclaration.AddEnumMember(enumMember);
        }

        return Task.FromResult(File()
            .AddNameSpace(NameSpace("TedToolkit.Occt")
                .AddMember(enumDeclaration))
            .ToCode());
    }
}