namespace TedToolkit.Occt.Generator.Options;

public sealed record DeclOptions
{
    public string FileName { get; }

    public DeclOptions(Enum @enum)
    {
        FileName = @enum.ToString();
    }
}