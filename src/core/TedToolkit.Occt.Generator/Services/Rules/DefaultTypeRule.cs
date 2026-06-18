using ClangSharp;

using Cysharp.Text;

using TedToolkit.Occt.Generator.Models;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services.Rules;

public class DefaultTypeRule : ITypeRule
{
    public bool TryResolve(ClangSharp.Type type, out TypeModel recordModel, out CXXRecordDecl? decl)
    {
        decl = type.AsCXXRecordDecl;

        recordModel = new TypeModel
        {
            SourceType = type.AsString,
            CppInteropType = type.AsString,
            CSharpPInvokeType = new(type.AsString),
            CSharpPublicType = new(ToValidCSharpName(type.AsString)),
        };

        return true;
    }

    private static string ToValidCSharpName(string name)
    {
        using var builder = ZString.CreateStringBuilder();
        var last = false;
        foreach (var c in name)
        {
            if (builder.Length is 0 && char.IsNumber(c))
            {
                AppendUnderscore();
            }
            else if (char.IsLetterOrDigit(c))
            {
                Append(c);
            }
            else
            {
                AppendUnderscore();
            }
        }

        if (last)
        {
            builder.Remove(builder.Length - 1, 1);
        }

        return builder.ToString();

        void Append(char c)
        {
            builder.Append(c);
            last = false;
        }

        void AppendUnderscore()
        {
            if (last)
            {
                return;
            }

            builder.Append('_');
            last = true;
        }
    }
}