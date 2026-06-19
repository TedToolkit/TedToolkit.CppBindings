// -----------------------------------------------------------------------
// <copyright file="RecordLayoutModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cysharp.Text;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Context.Domains;
using ModularPipelines.Modules;
using ModularPipelines.Options;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Modules;

/// <summary>
/// Prepares native C++ record layout data before generation starts.
/// </summary>
/// <param name="recordManager">The record queue manager.</param>
[DependsOn<ParseModule>]
public sealed class RecordLayoutModule(
    IOptions<GenerationOptions> generationOptions,
    IRecordModelManager recordManager,
    IVcpkgService vcpkgService) : Module<bool>
{
    private const string PROBE_FOLDER_NAME = "layout_probe";

    /// <inheritdoc />
    protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compile = new CppCompileCoontext(generationOptions.Value.CppFolder, PROBE_FOLDER_NAME,
            await vcpkgService.GetOcctCppVersionAsync().ConfigureAwait(false));

        await compile.AddSourceAsync(PROBE_FOLDER_NAME, GenerateProbe(), cancellationToken).ConfigureAwait(false);

        var folder = await compile.BuildAsync(context.Shell, true, vcpkgService.GetRoot(), vcpkgService.GetTriplet(),
                cancellationToken)
            .ConfigureAwait(false);
        var executableName = OperatingSystem.IsWindows()
            ? ZString.Concat(PROBE_FOLDER_NAME, ".exe")
            : PROBE_FOLDER_NAME;

        var probePath = Path.Combine(folder.FullName, executableName);

        var runningResult = await context.Shell.Command
            .ExecuteCommandLineTool(new GenericCommandLineToolOptions(probePath), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        ParseProbeOutput(runningResult.StandardOutput);
        return true;
    }

    private void ParseProbeOutput(string output)
    {
        var valueArray = output.Split('\n');
        var queue = new Queue<long>(valueArray[..^1].Select(long.Parse));
        foreach (var record in recordManager.RecordModels)
        {
            record.Size = queue.Dequeue();

            foreach (var field in record.FieldModels)
            {
                field.Offset = queue.Dequeue();
            }
        }
    }

    private string GenerateProbe()
    {
        var builder = ZString.CreateStringBuilder();
        builder.AppendLine("#include <cstddef>");
        builder.AppendLine("#include <iostream>");
        builder.AppendLine();
        builder.AppendLine("#define private public");
        builder.AppendLine("#define protected public");

        foreach (var fileName in generationOptions.Value.DeclOptions
                     .Select(o => o.FileName)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            builder.Append("#include <");
            builder.Append(fileName);
            builder.AppendLine(".hxx>");
        }

        builder.AppendLine("#undef protected");
        builder.AppendLine("#undef private");

        builder.AppendLine();
        builder.AppendLine("int main()");
        builder.AppendLine("{");

        foreach (var record in recordManager.RecordModels)
        {
            builder.Append("    std::cout << sizeof(");
            builder.Append(record.Type.CppTypeName);
            builder.AppendLine(") << '\\n';");

            foreach (var field in record.FieldModels)
            {
                builder.Append("    std::cout << offsetof(");
                builder.Append(record.Type.CppTypeName);
                builder.Append(", ");
                builder.Append(field.Name);
                builder.AppendLine(") << '\\n';");
            }
        }

        builder.AppendLine("    return 0;");
        builder.AppendLine("}");
        return builder.ToString();
    }
}