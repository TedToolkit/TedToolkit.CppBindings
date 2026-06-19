using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Models;

public class TypeModel
{
    /// <summary>
    /// Gets or sets the projected C++ type used in generated <c>extern "C"</c> wrappers.
    /// </summary>
    public required string CppTypeName { get; init; }

    /// <summary>
    /// Gets or sets the projected C# type used for PInvoke and field layout generation.
    /// </summary>
    public required DataType CSharpPInvokeType { get; init; }

    /// <summary>
    /// Gets or sets the projected public C# API type.
    /// </summary>
    public required DataType CSharpPublicType { get; init; }
}