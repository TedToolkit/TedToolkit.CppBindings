namespace TedToolkit.Occt.Generator.Generators;

public sealed class CppGenerator : IGenerator
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult("");
    }
}