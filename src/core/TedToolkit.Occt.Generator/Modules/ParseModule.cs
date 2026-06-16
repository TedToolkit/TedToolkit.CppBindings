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
    private const string RelayFileName = "main.cpp";

    private CXUnsavedFile CreateFile()
    {
        var stringBuilder = ZString.CreateStringBuilder();

        foreach (var valueDeclOption in generationOptions.Value.DeclOptions)
        {
            stringBuilder.Append("#include <");
            stringBuilder.Append(valueDeclOption.FileName);
            stringBuilder.AppendLine(".hxx>");
        }

        return CXUnsavedFile.Create(RelayFileName, stringBuilder.ToString());
    }

    private async Task<List<string>> CreateCommandLineArgsAsync()
    {
        return new List<string>(generationOptions.Value.CommandLineArgs)
        {
            ZString.Concat("-std=c++", await vcpkgService.GetOcctCppVersionAsync().ConfigureAwait(false)),
        };
    }

    protected override async Task<TranslationUnit?> ExecuteAsync(IModuleContext context,
        CancellationToken cancellationToken)
    {
        var commandLineArgs = await CreateCommandLineArgsAsync().ConfigureAwait(false);
        commandLineArgs.Add("-x");
        commandLineArgs.Add("c++");
        commandLineArgs.Add(ZString.Concat("-I", vcpkgService.GetOcctIncludeFolder()));
        commandLineArgs.Add(ZString.Concat("-I", vcpkgService.GetIncludeFolder()));

        using var file = CreateFile();

        // TODO: Memory leak by the index?
        var index = CXIndex.Create();
        var translationUnit = CXTranslationUnit.Parse(
            index,
            RelayFileName,
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
}
