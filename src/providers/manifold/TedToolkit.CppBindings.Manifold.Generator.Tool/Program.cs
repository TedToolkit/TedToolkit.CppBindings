using TedToolkit.CppBindings.Manifold.Generator;

if (args.Length != 2 || !string.Equals(args[0], "--output-root", StringComparison.Ordinal))
{
    return 1;
}

var provider = new ManifoldGenerationProvider();
await provider.GenerateAsync(new DirectoryInfo(args[1]), CancellationToken.None).ConfigureAwait(false);
return 0;
