using ClangSharp;

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class FieldService(IOptions<GenerationOptions> generationOption) : IFieldService
{
    public string GetName(FieldDecl decl)
    {
        return decl.Name;
    }

    public ClangSharp.Type GetType(FieldDecl decl)
    {
        return decl.Type;
    }

    public ValueTask<long> GetOffsetAsync(FieldDecl field)
    {
        var rawOffset = field.Handle.OffsetOfField;
        if (!generationOption.Value.GetFieldOffsetByRunning && rawOffset >= 0)
        {
            return ValueTask.FromResult(rawOffset / 8);
        }

        // TODO: Vcpkg running check for the offset? It needs the options.
        return ValueTask.FromResult(field.Handle.OffsetOfField / 8);
    }
}