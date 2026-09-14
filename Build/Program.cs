// See https://aka.ms/new-console-template for more information

using ModularPipelines.Extensions;

using Sourcy.DotNet;

using TedToolkit.ModularPipelines;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var repositoryDirectory = Projects.Build.Directory?.Parent
    ?? throw new InvalidOperationException("Unable to resolve the repository directory from the Build project.");
var solution = new FileInfo(Path.Combine(repositoryDirectory.FullName, "TedToolkit.CppBindings.slnx"));

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            solution,
        ],
        Solution = solution,
        TestFiles = [],
    },
    new FileInfo(Path.Combine(Projects.Build.Directory!.FullName, "appsettings.json")));

await pipeline
    .ExecuteAsync(static builder => builder
        .AddModule<WindowsBindingsModule>()
        .AddModule<NativeIntegrationModule>()
        .AddModule<ManagedTestGateModule>())
    .ConfigureAwait(false);