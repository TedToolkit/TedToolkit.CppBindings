using System.Text.Json;

using TedToolkit.CppBindings.Cgal;
using TedToolkit.CppBindings.Occt;

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

await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new
{
    Passed = true,
    CgalSquaredDistance = squaredDistance,
    OcctX = occtX,
    OcctY = occtY,
}));
return 0;
