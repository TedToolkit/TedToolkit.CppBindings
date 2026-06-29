using TedToolkit.Occt.Generator.Models;
using TedToolkit.RoslynHelper.Generators.Syntaxes;

namespace TedToolkit.Occt.Generator.Tests.Models.MethodModelTests;

internal sealed class GetMethodInteropNameTest
{
    [Test]
    public async Task Should_strip_const_qualifiers_from_parameter_type_names_Async()
    {
        var recordType = CreateType("gp_Pnt");
        var method = new MethodModel
        {
            DescriptionItems = [],
            ReturnTypeDescriptionItems = [],
            NoExceptions = true,
            IsConst = false,
            IsStatic = false,
            ReturnType = CreateType("void"),
            MethodName = "SetCoord",
            Type = MethodModelType.Normal,
            Parameters =
            [
                new ParameterModel
                {
                    DescriptionItems = [],
                    Name = "surface",
                    Type = CreateType("const Geom_Surface&"),
                },
                new ParameterModel
                {
                    DescriptionItems = [],
                    Name = "text",
                    Type = CreateType("char const *"),
                },
            ],
        };

        var record = new RecordModel
        {
            DescriptionItems = [],
            Base = null,
            IsAbstract = false,
            Type = recordType,
            Size = 0,
            FieldModels = [],
            MethodModels = [],
        };

        await Assert.That(method.GetMethodInteropName(record))
            .IsEqualTo("gp_Pnt_SetCoord_Geom_Surface_char");
    }

    private static TypeModel CreateType(string cppTypeName)
    {
        return new TypeModel
        {
            CppTypeName = cppTypeName,
            CSharpPInvokeType = DataType.Void,
            CSharpPublicType = DataType.Void,
        };
    }
}
