// -----------------------------------------------------------------------
// <copyright file="EnumGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using TedToolkit.Occt.Generator.Models.Declarations;
using TedToolkit.RoslynHelper.Generators;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    TedToolkit.Occt.Generator.Generators.EnumGenerator>;

namespace TedToolkit.Occt.Generator.Generators;

/// <summary>
/// Produces the generated C# enum for a parsed native enum declaration.
/// </summary>
/// <param name="enumModel">The enum declaration to generate.</param>
internal sealed class EnumGenerator(EnumModel enumModel) : IGenerator
{
    /// <inheritdoc/>
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