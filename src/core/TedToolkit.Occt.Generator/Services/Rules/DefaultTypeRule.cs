using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services.Rules;

public class DefaultTypeRule : ITypeRule
{
    public bool TryResolve(ClangSharp.Type type, out TypeResolveResult result)
    {
        ArgumentNullException.ThrowIfNull(type);

        result = new()
        {
            Decl = type.AsCXXRecordDecl,
            Type = new()
                {
                    CppTypeName = type.AsString,
                    CSharpPInvokeType = type.ToPInvokeDataType(),
                    CSharpPublicType = type.ToPublicDataType(),
                },
        };

        return true;
    }
}