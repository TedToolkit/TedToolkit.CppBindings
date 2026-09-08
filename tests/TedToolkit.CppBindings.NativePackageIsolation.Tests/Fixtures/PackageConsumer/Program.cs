using System.Text.Json;

using TedToolkit.CppBindings.Cgal;
using TedToolkit.CppBindings.Occt;
#if INCLUDE_MANIFOLD
using TedToolkit.CppBindings.Manifold;
#endif

if (args.Length != 1)
{
    return 1;
}

var cgalOrigin = Point_2Extensions.Create(0, 0);
var cgalPoint = Point_2Extensions.Create(3, 4);
var squaredDistance = CgalKernel.SquaredDistance(cgalOrigin, cgalPoint);

var occtPoint = gp_Pnt2dExtensions.Create(7, 11);
var occtX = occtPoint.X();
var occtY = occtPoint.Y();
if (squaredDistance != 25 || occtX != 7 || occtY != 11)
{
    return 2;
}

#if INCLUDE_MANIFOLD
double[] manifoldVertices =
[
    0, 0, 0,
    1, 0, 0,
    0, 1, 0,
    0, 0, 1,
];
ulong[] manifoldTriangles = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
using var manifold = Manifold.Create(manifoldVertices, manifoldTriangles);
var manifoldStatus = manifold.Status().ToString();
var manifoldTriangleCount = (ulong)manifold.NumTri();
if (manifoldStatus != "NoError" || manifoldTriangleCount != 4)
{
    return 3;
}
#else
string? manifoldStatus = null;
ulong? manifoldTriangleCount = null;
#endif

await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new
{
    Passed = true,
    CgalSquaredDistance = squaredDistance,
    OcctX = occtX,
    OcctY = occtY,
    ManifoldStatus = manifoldStatus,
    ManifoldTriangleCount = manifoldTriangleCount,
}));
return 0;
