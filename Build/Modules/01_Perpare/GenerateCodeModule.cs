using System.Runtime.CompilerServices;

using Cysharp.Text;

using ModularPipelines.Context;

using Sourcy.DotNet;

using TedToolkit.ModularPipelines.Modules;
using TedToolkit.RoslynHelper.Generators;

using static TedToolkit.RoslynHelper.Generators.SourceComposer;
using static TedToolkit.RoslynHelper.Generators.SourceComposer<
    Build.Modules.GenerateCodeModule>;

namespace Build.Modules;

public sealed class GenerateCodeModule : PrepareModule<bool>
{
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var directoryInfo = Projects.TedToolkit_Occt_Runtime.Directory?.CreateSubdirectory("Properties");
        if (directoryInfo is null)
        {
            return false;
        }

        var file = File();

        foreach (var s in await GetTripletsAsync(cancellationToken).ConfigureAwait(false))
        {
            file.AddAttribute(Attribute<InternalsVisibleToAttribute>()
                .AddModifier(AttributeModifier.ASSEMBLY)
                .AddArgument(Argument(ZString.Concat("TedToolkit.Occt.", s).ToLiteral())));
        }
        await System.IO.File.WriteAllTextAsync(Path.Combine(directoryInfo.FullName, "AssemblyInfo.g.cs"), file.ToCode(), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static async Task<IEnumerable<string>> GetTripletsAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(vcpkgRoot))
        {
            return [];
        }

        var tripletDirectories = new[]
        {
            Path.Combine(vcpkgRoot, "triplets"),
            Path.Combine(vcpkgRoot, "triplets", "community"),
        };

        var triplets = tripletDirectories
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cmake", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static triplet => !string.IsNullOrWhiteSpace(triplet))
            .Where(static triplet => !triplet!.Contains("xbox", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(static triplet => triplet!)
            .ToArray();

        return await Task.FromResult<IEnumerable<string>>(triplets).ConfigureAwait(false);
    }
}
