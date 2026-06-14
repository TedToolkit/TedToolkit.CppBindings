using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Context;
using ModularPipelines.Modules;
using ModularPipelines.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

public sealed class ParseModule(
    IOptions<GenerationOptions> generationOptions,
    IVcpkgService vcpkgService) : Module<TranslationUnit>
{
    private const string RELAY_FILE = "main.cpp";

    private CXUnsavedFile CreateFile()
    {
        var stringBuilder = ZString.CreateStringBuilder();
        foreach (var valueDeclOption in generationOptions.Value.DeclOptions)
        {
            stringBuilder.Append("#include <");
            stringBuilder.Append(valueDeclOption.FileName);
            stringBuilder.AppendLine(".hxx>");
        }

        return CXUnsavedFile.Create(RELAY_FILE, stringBuilder.ToString());
    }

    protected override async Task<TranslationUnit?> ExecuteAsync(IModuleContext context,
        CancellationToken cancellationToken)
    {
        var commandLineArgs = new List<string>()
        {
            ZString.Concat("-I", vcpkgService.GetOcctIncludeFolder()),
            ZString.Concat("-I", vcpkgService.GetIncludeFolder()),
        };
        var systemArguments = await GetSystemArguments(context, cancellationToken).ConfigureAwait(false);
        foreach (var argument in systemArguments)
        {
            commandLineArgs.Add("-isystem");
            commandLineArgs.Add(argument);
        }

        using var file = CreateFile();

        // TODO: Memory leak by the index?
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RELAY_FILE,
            CollectionsMarshal.AsSpan(commandLineArgs),
            [file],
            CXTranslationUnit_Flags.CXTranslationUnit_None);

        return TranslationUnit.GetOrCreate(translationUnit);
    }

    private async Task<IEnumerable<string>> GetSystemArguments(IModuleContext context,
        CancellationToken cancellationToken)
    {
        var nullFile = OperatingSystem.IsWindows()
            ? "NUL"
            : "/dev/null";

        var result = await context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("clang++") { Arguments = ["-E", "-x", "c++", nullFile, "-v"], },
            new CommandExecutionOptions() { ThrowOnNonZeroExitCode = true, },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.StandardError
            .Split(Environment.NewLine)
            .SkipWhile(s => s != "#include <...> search starts here:")
            .Skip(1)
            .TakeWhile(s => s != "End of search list.");
    }
}