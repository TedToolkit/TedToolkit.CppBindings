// -----------------------------------------------------------------------
// <copyright file="ManagedTestGateModule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

using Microsoft.Extensions.Options;

using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;

using TedToolkit.ModularPipelines;
using TedToolkit.ModularPipelines.Constants;
using TedToolkit.ModularPipelines.Modules;
using TedToolkit.ModularPipelines.Options;

/// <summary>
/// Runs every repository-managed test project and rejects incomplete or unsuccessful TRX results.
/// </summary>
/// <param name="dotnet">The .NET pipeline options.</param>
/// <param name="files">The repository pipeline files.</param>
[DependsOn<NativeIntegrationModule>]
public sealed class ManagedTestGateModule(
    IOptions<DotNetPipelineOptions> dotnet,
    PipelineFiles files) : CompileCheckModule<FileInfo[]>
{
    /// <inheritdoc />
    protected override ModuleConfiguration Configure()
    {
        return ModuleConfiguration.Create().WithRetryCount(0).Build();
    }

    /// <inheritdoc />
    protected override async Task<FileInfo[]?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        var root = files.Solution.Directory
            ?? throw new InvalidOperationException("The repository root could not be resolved.");
        var projects = new[]
        {
            "tests/TedToolkit.CppBindings.Generator.Tests/TedToolkit.CppBindings.Generator.Tests.csproj",
            "tests/TedToolkit.CppBindings.Cgal.Generator.Tests/TedToolkit.CppBindings.Cgal.Generator.Tests.csproj",
            "tests/TedToolkit.CppBindings.Cgal.Runtime.Tests/TedToolkit.CppBindings.Cgal.Runtime.Tests.csproj",
            "tests/TedToolkit.CppBindings.Manifold.Generator.Tests/TedToolkit.CppBindings.Manifold.Generator.Tests.csproj",
            "tests/TedToolkit.CppBindings.Manifold.Runtime.Tests/TedToolkit.CppBindings.Manifold.Runtime.Tests.csproj",
            "tests/TedToolkit.CppBindings.Occt.Generator.Tests/TedToolkit.CppBindings.Occt.Generator.Tests.csproj",
            "tests/TedToolkit.CppBindings.Runtime.Tests/TedToolkit.CppBindings.Runtime.Tests.csproj",
            "tests/TedToolkit.CppBindings.Analyzers.Tests/TedToolkit.CppBindings.Analyzers.Tests.csproj",
        }.Select(path => new FileInfo(Path.Combine(root.FullName, path))).ToArray();
        var reports = new List<FileInfo>();
        foreach (var project in projects)
        {
            reports.Add(await RunProjectAsync(
                    project,
                    dotnet.Value.Configuration,
                    context.GetTestFolder().Path,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        return reports.ToArray();
    }

    private static async Task<FileInfo> RunProjectAsync(
        FileInfo project,
        string configuration,
        string resultFolder,
        CancellationToken cancellationToken)
    {
        var projectDirectory = project.Directory
            ?? throw new InvalidOperationException($"The directory for {project.FullName} could not be resolved.");
        var reportName = $"build-gate-{Path.GetFileNameWithoutExtension(project.Name)}.trx";
        foreach (var staleReport in projectDirectory.GetFiles(reportName, SearchOption.AllDirectories))
        {
            staleReport.Delete();
        }

        await BuildProcess.RunAsync(
                "dotnet",
                [
                    "run", "--project", project.FullName, "--configuration", configuration,
                    "--no-build", "--no-restore", "--", "--report-trx", "--report-trx-filename", reportName,
                ],
                projectDirectory.FullName,
                cancellationToken)
            .ConfigureAwait(false);

        var projectReports = projectDirectory.GetFiles(reportName, SearchOption.AllDirectories);
        if (projectReports.Length is not 1)
        {
            throw new InvalidOperationException(
                $"Expected one TRX report for {project.Name}, but found {projectReports.Length}.");
        }

        EnsureSuccessful(projectReports[0]);
        var resultPath = Path.Combine(resultFolder, $"{Path.GetFileNameWithoutExtension(project.Name)}_{reportName}");
        File.Copy(projectReports[0].FullName, resultPath, true);
        return new FileInfo(resultPath);
    }

    private static void EnsureSuccessful(FileInfo report)
    {
        var counters = XDocument.Load(report.FullName)
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName is "Counters")
            ?? throw new InvalidOperationException($"TRX report {report.FullName} has no Counters element.");
        var total = ReadCounter(counters, "total");
        var executed = ReadCounter(counters, "executed");
        var passed = ReadCounter(counters, "passed");
        if (total is not 0 && executed == total && passed == total)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Tests in {report.Name} did not all pass: total={total}, executed={executed}, passed={passed}.");
    }

    private static int ReadCounter(XElement counters, string name)
    {
        var value = counters.Attribute(name)?.Value
            ?? throw new InvalidOperationException($"TRX Counters has no {name} attribute.");
        return int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
