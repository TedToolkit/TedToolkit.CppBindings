namespace TedToolkit.Occt.Generator.Options;

public sealed record GenerationOptions(
    bool GetFieldOffsetByRunning,
    IReadOnlyList<DeclOptions> DeclOptions);