namespace TedToolkit.Occt.Generator.Options;

public sealed record GenerationOptions()
{
    public bool GetFieldOffsetByRunning { get; init; }
    public required IReadOnlyList<DeclOptions> DeclOptions { get; init; }

    public IReadOnlyList<string> CommandLineArgs { get; init; }

    public required DirectoryInfo CSharpFolder { get; init; }
    public required DirectoryInfo CppFolder { get; init; }

    public bool IsInternal { get; init; }
}