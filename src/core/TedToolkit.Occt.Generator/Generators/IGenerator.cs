namespace TedToolkit.Occt.Generator.Generators;

public interface IGenerator
{
    Task<string> GenerateAsync();
}