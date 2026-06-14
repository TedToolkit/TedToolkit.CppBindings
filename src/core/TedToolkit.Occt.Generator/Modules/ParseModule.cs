using System.Runtime.InteropServices;

using ClangSharp;
using ClangSharp.Interop;

using Cysharp.Text;

using Microsoft.Extensions.Logging;
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
        foreach (var valueDeclOption in new DirectoryInfo(vcpkgService.GetOcctIncludeFolder()).EnumerateFiles("*.hxx"))
        {
            stringBuilder.Append("#include <");
            stringBuilder.Append(valueDeclOption.Name);
            stringBuilder.AppendLine(">");
        }

        return CXUnsavedFile.Create(RELAY_FILE, stringBuilder.ToString());
    }

    protected override async Task<TranslationUnit?> ExecuteAsync(IModuleContext context,
        CancellationToken cancellationToken)
    {
        var commandLineArgs = new List<string>(generationOptions.Value.CommandLineArgs)
        {
            "-x",
            "c++",
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

        for (uint i = 0; i < translationUnit.NumDiagnostics; i++)
        {
            using var cxDiagnostic = translationUnit.GetDiagnostic(i);
            var errorMessage = cxDiagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString();
            switch (cxDiagnostic.Severity)
            {
#pragma warning disable CA1848, CA2254
                case CXDiagnosticSeverity.CXDiagnostic_Ignored:
                    context.Logger.LogDebug(errorMessage);
                    break;
                case CXDiagnosticSeverity.CXDiagnostic_Note:
                    context.Logger.LogInformation(errorMessage);
                    break;
                case CXDiagnosticSeverity.CXDiagnostic_Warning:
                    context.Logger.LogWarning(errorMessage);
                    break;
                case CXDiagnosticSeverity.CXDiagnostic_Error:
                    context.Logger.LogError(errorMessage);
                    break;
                case CXDiagnosticSeverity.CXDiagnostic_Fatal:
                    context.Logger.LogCritical(errorMessage);
                    break;
#pragma warning restore CA1848, CA2254
            }
        }

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
            .TakeWhile(s => s != "End of search list.")
            .Select(s => s.Trim());
    }
}