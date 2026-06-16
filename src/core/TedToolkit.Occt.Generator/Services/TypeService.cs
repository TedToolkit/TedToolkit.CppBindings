using ClangSharp;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class TypeService : ITypeService
{
    public string GetCppName(ClangSharp.Type type)
    {
        var result = type.AsString;

        if (string.IsNullOrEmpty(result))
        {
            throw new InvalidOperationException("Field type name cannot be empty. Please check your arguments.");
        }

        return result;
    }

    public string GetCSharpName(ClangSharp.Type type)
    {
        return GetCppName(type)
            .Replace("::", "_", StringComparison.InvariantCulture)
            .Replace('<', '_')
            .Replace('>', '_')
            .Trim('_');
    }

    public ClangSharp.Type DesugarType(ClangSharp.Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.CanonicalType;
    }
}