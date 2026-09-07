using System.Text.Json;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Cgal;

if (args.Length != 1)
{
    return 1;
}

NativeError carrier = default;
var unknownResult = new CgalUnknownResultException("unknown result");
var generatedNone = GeneratedIntersection.FromNative(default);
var generatedPoint = GeneratedIntersection.FromNative(new(1, 1.0, 2.0, 0.0, 0.0));
var runtimeAssembly = typeof(CgalException).Assembly;
var publicTypes = runtimeAssembly.GetExportedTypes()
    .Select(static type => type.FullName)
    .Where(static name => name is not null)
    .Order(StringComparer.Ordinal)
    .ToArray();
var references = runtimeAssembly.GetReferencedAssemblies()
    .Select(static assembly => assembly.Name)
    .Where(static name => name is not null)
    .Order(StringComparer.Ordinal)
    .ToArray();

var expected = new[]
{
    typeof(CgalException),
    typeof(CgalFailureException),
    typeof(CgalErrorException),
    typeof(CgalPreconditionException),
    typeof(CgalPostconditionException),
    typeof(CgalAssertionException),
    typeof(CgalTestException),
    typeof(CgalWarningException),
    typeof(CgalArgumentException),
    typeof(CgalArgumentOutOfRangeException),
    typeof(CgalOutOfMemoryException),
    typeof(CgalArithmeticException),
    typeof(CgalStandardException),
    typeof(CgalUnknownException),
    typeof(CgalUnknownResultException),
    typeof(NativeErrorProjection),
};

if (carrier.Kind != 0
    || unknownResult.Message != "unknown result"
    || generatedNone.Kind != GeneratedIntersectionKind.None
    || !generatedPoint.TryGetPoint(out var point)
    || point != new GeneratedPoint(1.0, 2.0)
    || expected.Any(type => !publicTypes.Contains(type.FullName, StringComparer.Ordinal))
    || references.Any(name => name!.Contains("Occt", StringComparison.OrdinalIgnoreCase))
    || references.Any(name => name!.Contains("Generator", StringComparison.OrdinalIgnoreCase)))
{
    return 2;
}

await File.WriteAllTextAsync(
    args[0],
    JsonSerializer.Serialize(new
    {
        Assembly = runtimeAssembly.GetName().Name,
        ExportedTypes = publicTypes,
        References = references,
    }));
return 0;
