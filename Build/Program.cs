// See https://aka.ms/new-console-template for more information

using Sourcy.DotNet;

using TedToolkit.ModularPipelines;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            Solutions.TedToolkit_Occt,
        ],
        Solution = Solutions.TedToolkit_Occt,
        TestFiles =
        [
        ],
    },
    new FileInfo(Path.Combine(Projects.Build.Directory!.FullName, "appsettings.json")));

await pipeline.ExecuteAsync().ConfigureAwait(false);