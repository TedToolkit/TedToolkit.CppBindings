using TedToolkit.CppBindings.Fcl.Generator;

if (args.Length != 2 || !string.Equals(args[0], "--output-root", StringComparison.Ordinal))
{
    return 1;
}

await new FclGenerationProvider().GenerateAsync(new DirectoryInfo(args[1]), CancellationToken.None)
    .ConfigureAwait(false);
return 0;
