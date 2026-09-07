using System.Text.Json;

using TedToolkit.CppBindings.Cgal;

if (args.Length != 1)
{
    return 1;
}

var origin2 = Point_2Extensions.Create(0, 0);
var point2 = Point_2Extensions.Create(3, 4);
var squaredDistance2 = CgalKernel.SquaredDistance(origin2, point2);

var origin3 = Point_3Extensions.Create(0, 0, 0);
var point3 = Point_3Extensions.Create(1, 2, 2);
var squaredDistance3 = CgalKernel.SquaredDistance(origin3, point3);

var horizontal = Segment_2Extensions.Create(
    Point_2Extensions.Create(-1, 0),
    Point_2Extensions.Create(2, 0));
var vertical = Segment_2Extensions.Create(
    Point_2Extensions.Create(1, -1),
    Point_2Extensions.Create(1, 1));
var intersection = CgalKernel.Intersect(horizontal, vertical);
if (!intersection.TryGetPoint(out var intersectionPoint))
{
    return 2;
}

var disjoint = CgalKernel.Intersect(
    Segment_2Extensions.Create(Point_2Extensions.Create(0, 0), Point_2Extensions.Create(1, 0)),
    Segment_2Extensions.Create(Point_2Extensions.Create(0, 1), Point_2Extensions.Create(1, 1)));

CgalPreconditionException? precondition = null;
try
{
    _ = point2.Cartesian(2);
}
catch (CgalPreconditionException exception)
{
    precondition = exception;
}

if (precondition is null)
{
    return 3;
}

var result = new
{
    SquaredDistance2 = squaredDistance2,
    SquaredDistance3 = squaredDistance3,
    IntersectionKind = intersection.Kind.ToString(),
    IntersectionX = intersectionPoint.X(),
    IntersectionY = intersectionPoint.Y(),
    EmptyIntersectionKind = disjoint.Kind.ToString(),
    PreconditionType = precondition.NativeTypeName,
    PreconditionMessage = precondition.Message,
    PreconditionStack = precondition.NativeStackTrace,
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true,
}));
return 0;
