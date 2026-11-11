using System.Runtime.InteropServices;

#if OCCT
using TedToolkit.CppBindings.Occt;
#endif
#if CGAL
using TedToolkit.CppBindings.Cgal;
#endif
#if MANIFOLD
using TedToolkit.CppBindings.Manifold;
#endif
#if FCL
using TedToolkit.CppBindings.Fcl;
#endif
#if SMLIB
using TedToolkit.CppBindings.Smlib;
#endif

if (Environment.Version.Major != 8 || RuntimeInformation.RuntimeIdentifier != args.Single())
{
    throw new InvalidOperationException("The consumer must run on .NET 8 and the package's target RID.");
}

#if OCCT
var point = gp_Pnt2d.Create(7, 11);
var color = Quantity_Color.Create();
using (var line = Prs3d_LineAspect.Create(in color, Aspect_TypeOfLine.Aspect_TOL_SOLID, 2.5))
{
    if (point.X() != 7 || point.Y() != 11)
    {
        throw new InvalidOperationException("The OCCT point coordinates did not round trip.");
    }
}
#endif

#if CGAL
var origin = Point_2.Create(0, 0);
var target = Point_2.Create(3, 4);
if (CgalKernel.SquaredDistance(origin, target) != 25)
{
    throw new InvalidOperationException("The CGAL distance did not match the fixture.");
}
try
{
    _ = target.Cartesian(2);
    throw new InvalidOperationException("CGAL accepted an out-of-range Cartesian index.");
}
catch (CgalPreconditionException)
{
}
#endif

#if MANIFOLD
double[] vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1];
ulong[] triangles = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
using (var manifold = Manifold.Create(vertices, triangles))
{
    if (manifold.Status().ToString() != "NoError" || manifold.NumTri() != 4)
    {
        throw new InvalidOperationException("Manifold did not construct the fixture tetrahedron.");
    }
}
#endif

#if FCL
double[] collisionVertices = [0, 0, 0, 1, 0, 0, 0, 1, 0];
nuint[] collisionTriangles = [0, 1, 2];
var build = FclBvhModel.Create(collisionVertices, collisionTriangles);
using (var model = build.Model ?? throw new InvalidOperationException("FCL did not construct a model."))
{
    var collision = FclContinuousCollision.Query(model, model, new FclVector3(1, 0, 0));
    if (build.Code.ToString() != "BVH_OK" || !collision.IsCollide || collision.TimeOfContact != 0)
    {
        throw new InvalidOperationException("The FCL model did not collide with itself.");
    }
}
#endif

#if SMLIB
using (var box = Solids.CreateBox(1, 2, 3, 2, 3, 4))
{
    var volume = box.ComputeVolume(1e-7);
    if (!double.IsFinite(volume) || Math.Abs(volume - 24) > 1e-6)
    {
        throw new InvalidOperationException("The SMLib box volume did not match the fixture.");
    }
}
#endif

Console.WriteLine($"Verified the packaged native bindings on {args[0]}.");
