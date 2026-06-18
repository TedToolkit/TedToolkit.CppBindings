using ClangSharp;

using TedToolkit.Occt.Generator.Models;

namespace TedToolkit.Occt.Generator.Services.Interfaces;

public interface IResolver
{
    TypeModel Resolve(ClangSharp.Type type, out CXXRecordDecl? decl);
}