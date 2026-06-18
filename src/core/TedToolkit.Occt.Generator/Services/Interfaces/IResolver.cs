using ClangSharp;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IResolver
{
    TypeResolveResult Resolve(ClangSharp.Type type);
}
