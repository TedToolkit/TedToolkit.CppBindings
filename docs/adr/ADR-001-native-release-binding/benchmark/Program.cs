using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace TedToolkit.Occt.NativeRelease.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        NativeFixture.ValidateConfiguration();

        IConfig config = DefaultConfig.Instance
            .AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithId("Measured"))
            .AddColumn(StatisticColumn.Median, StatisticColumn.P95)
            .AddExporter(JsonExporter.Full);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
