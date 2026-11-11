// See https://aka.ms/new-console-template for more information

using ModularPipelines.Extensions;

using Sourcy.DotNet;

using TedToolkit.FileSystem;
using TedToolkit.ModularPipelines;
using TedToolkit.ModularPipelines.Modules;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var buildDirectory = Projects.Build.Directory?.ToPath()
    ?? throw new InvalidOperationException("Unable to resolve the Build project directory.");
var repositoryDirectory = buildDirectory.TrimEndingDirectorySeparator().Parent
    ?? throw new InvalidOperationException("Unable to resolve the repository directory from the Build project.");
var solution = repositoryDirectory / new FileName("TedToolkit.CppBindings.slnx");

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            solution.ToFileInfo(),
        ],
        Solution = solution.ToFileInfo(),
        TestFiles = [],
    },
    (buildDirectory / new FileName("appsettings.json")).ToFileInfo());

await pipeline
    .CreateNoModules()
    .AddModule<CleanOutputModule>()
    .AddModule<UpdateEditorConfigModule>()
    .AddModule<FormatAllCodeModule>()
    .AddModule<WindowsBindingsModule>()
    .AddModule<DotnetBuildModule>()
    .AddModule<NativeIntegrationModule>()
    .AddModule<ManagedTestGateModule>()
    .AddModule<AssertBuildTestModule>()
    .ExecutePipelineAsync()
    .ConfigureAwait(false);
