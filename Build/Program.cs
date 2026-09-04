// See https://aka.ms/new-console-template for more information

using ModularPipelines.Extensions;

using Sourcy.DotNet;

using TedToolkit.ModularPipelines;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            Solutions.TedToolkit_CppBindings,
        ],
        Solution = Solutions.TedToolkit_CppBindings,
        TestFiles = [],
    },
    new FileInfo(Path.Combine(Projects.Build.Directory!.FullName, "appsettings.json")));

await pipeline
    .ExecuteAsync(static builder => builder
        .AddModule<WindowsBindingsModule>()
        .AddModule<NativeIntegrationModule>()
        .AddModule<ManagedTestGateModule>())
    .ConfigureAwait(false);