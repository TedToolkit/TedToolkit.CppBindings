namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IVcpkgService
{
    string GetRoot();

    string GetIncludeFolder();

    string GetOcctIncludeFolder();

    string GetTriplet();

    Task<int> GetOcctCppVersionAsync();
}
