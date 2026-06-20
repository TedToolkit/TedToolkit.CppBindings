using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Models.MethodModelTests;

internal sealed class GetInvokeNameTest
{
    [Test]
    public async Task Should_keep_normal_method_name_Async()
    {
        var model = CreateMethod(MethodModelType.Normal, "Coord");

        await Assert.That(model.GetInvokeName()).IsEqualTo("Coord");
    }

    [Test]
    public async Task Should_normalize_operator_symbols_Async()
    {
        var model = CreateMethod(MethodModelType.Operator, "operator==");

        await Assert.That(model.GetInvokeName()).IsEqualTo("operatorEqualEqual");
    }

    [Test]
    public async Task Should_normalize_conversion_operator_spacing_Async()
    {
        var model = CreateMethod(MethodModelType.Explicit, "operator Standard_Real");

        await Assert.That(model.GetInvokeName()).IsEqualTo("operatorStandard_Real");
    }

    private static MethodModel CreateMethod(MethodModelType type, string methodName)
    {
        var voidType = new TypeModel
        {
            CppTypeName = "void",
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };

        return new MethodModel
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = false,
            ReturnType = voidType,
            MethodName = methodName,
            Type = type,
            Parameters = [],
        };
    }
}
