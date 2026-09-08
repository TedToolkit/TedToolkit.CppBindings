using System.Text.Json;
using System.Globalization;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Manifold;

if (args.Length != 1)
{
    return 2;
}

double[] tetraVertices =
[
    0, 0, 0,
    1, 0, 0,
    0, 1, 0,
    0, 0, 1,
];
ulong[] tetraTriangles =
[
    0, 2, 1,
    0, 1, 3,
    0, 3, 2,
    1, 2, 3,
];

using var tetrahedron = Manifold.Create(tetraVertices, tetraTriangles);
using var translated = tetrahedron.Translate(0.125, 0.0, 0.0);
using var union = tetrahedron.Boolean(translated, ManifoldOp.Add);
using var intersection = tetrahedron.Boolean(translated, ManifoldOp.Intersect);
using var difference = tetrahedron.Boolean(translated, ManifoldOp.Subtract);
using var translatedAgain = tetrahedron.Translate(0.0, 0.125, 0.0);
using var foldedOnce = tetrahedron.Boolean(translated, ManifoldOp.Add);
using var foldedTwice = foldedOnce.Boolean(translatedAgain, ManifoldOp.Add);
var mesh = translated.GetMesh();
using var firstBox = CreateBox(0, 0, 0, 1, 1, 1);
using var secondBox = CreateBox(0.5, 0, 0, 1.5, 1, 1);
using var boxUnion = firstBox.Boolean(secondBox, ManifoldOp.Add);
using var boxIntersection = firstBox.Boolean(secondBox, ManifoldOp.Intersect);
using var boxDifference = firstBox.Boolean(secondBox, ManifoldOp.Subtract);

var nonFiniteVertices = (double[])tetraVertices.Clone();
nonFiniteVertices[0] = double.NaN;
using var nonFinite = Manifold.Create(nonFiniteVertices, tetraTriangles);
var outOfRangeTriangles = (ulong[])tetraTriangles.Clone();
outOfRangeTriangles[0] = 99;
using var outOfRange = Manifold.Create(tetraVertices, outOfRangeTriangles);
using var moving = tetrahedron.Translate(2.0, 0.0, 0.0);
var initialContact = ContinuousCollision(tetrahedron, tetrahedron, -1.0, 0.0, 0.0, 1e-6);
var endpointMiss = ContinuousCollision(tetrahedron, moving, 1.0, 0.0, 0.0, 1e-6);
var detectedContact = ContinuousCollision(tetrahedron, moving, -1.5, 0.0, 0.0, 1e-6);

var result = new
{
    Status = tetrahedron.Status().ToString(),
    TriangleCount = (ulong)tetrahedron.NumTri(),
    TranslatedStatus = translated.Status().ToString(),
    UnionStatus = union.Status().ToString(),
    UnionTriangleCount = (ulong)union.NumTri(),
    IntersectionStatus = intersection.Status().ToString(),
    IntersectionTriangleCount = (ulong)intersection.NumTri(),
    DifferenceStatus = difference.Status().ToString(),
    DifferenceTriangleCount = (ulong)difference.NumTri(),
    FoldedTriangleCount = (ulong)foldedTwice.NumTri(),
    EmptyFoldTriangleCount = (ulong)tetrahedron.NumTri(),
    MeshVertexCoordinateCount = mesh.VertexCoordinates.Length,
    MeshTriangleIndexCount = mesh.TriangleIndices.Length,
    MeshMinX = mesh.VertexCoordinates.Where((_, index) => index % 3 == 0).Min(),
    NonFiniteStatus = nonFinite.Status().ToString(),
    OutOfRangeStatus = outOfRange.Status().ToString(),
    InitialContact = initialContact,
    EndpointMissIsNaN = double.IsNaN(endpointMiss),
    DetectedContact = detectedContact,
    TetraOraclePassed = MatchesCanonicalTriangles(
        tetrahedron.GetMesh(),
        "0,0,0;0,0,1;0,1,0|0,0,0;0,1,0;1,0,0|0,0,0;1,0,0;0,0,1|0,0,1;1,0,0;0,1,0"),
    BoxUnionOraclePassed = MatchesCanonicalTriangles(
        boxUnion.GetMesh(),
        "0,0,0;0,0,1;0,1,1|0,0,0;0,1,0;0.5,0.5,0|0,0,0;0,1,1;0,1,0|0,0,0;0.5,0,0;0.5,0,1|0,0,0;0.5,0,1;0,0,1|0,0,0;0.5,0.5,0;0.5,0,0|0,0,1;0.5,0,1;0.5,0.5,1|0,0,1;0.5,0.5,1;0,1,1|0,1,0;0,1,1;0.5,1,0|0,1,0;0.5,1,0;0.5,0.5,0|0,1,1;0.5,0.5,1;0.5,1,1|0,1,1;0.5,1,1;0.5,1,0|0.5,0,0;0.5,0.5,0;1,0.5,0|0.5,0,0;1,0.5,0;1.5,0,0|0.5,0,0;1.5,0,0;1.5,0,1|0.5,0,0;1.5,0,1;0.5,0,1|0.5,0,1;1,0.5,1;0.5,0.5,1|0.5,0,1;1.5,0,1;1,0.5,1|0.5,0.5,0;0.5,1,0;1,0.5,0|0.5,0.5,1;1,0.5,1;0.5,1,1|0.5,1,0;0.5,1,1;1.5,1,1|0.5,1,0;1.5,1,0;1,0.5,0|0.5,1,0;1.5,1,1;1.5,1,0|0.5,1,1;1,0.5,1;1.5,1,1|1,0.5,0;1.5,1,0;1.5,0,0|1,0.5,1;1.5,0,1;1.5,1,1|1.5,0,0;1.5,1,0;1.5,1,1|1.5,0,0;1.5,1,1;1.5,0,1"),
    BoxIntersectionOraclePassed = MatchesCanonicalTriangles(
        boxIntersection.GetMesh(),
        "0.5,0,0;0.5,0,1;0.5,1,1|0.5,0,0;0.5,1,0;1,0,0|0.5,0,0;0.5,1,1;0.5,1,0|0.5,0,0;1,0,0;1,0,1|0.5,0,0;1,0,1;0.5,0,1|0.5,0,1;1,0,1;1,1,1|0.5,0,1;1,1,1;0.5,1,1|0.5,1,0;0.5,1,1;1,1,0|0.5,1,0;1,1,0;1,0,0|0.5,1,1;1,1,1;1,1,0|1,0,0;1,1,0;1,1,1|1,0,0;1,1,1;1,0,1"),
    BoxDifferenceOraclePassed = MatchesCanonicalTriangles(
        boxDifference.GetMesh(),
        "0,0,0;0,0,1;0,1,1|0,0,0;0,1,0;0.5,0,0|0,0,0;0,1,1;0,1,0|0,0,0;0.5,0,0;0.5,0,1|0,0,0;0.5,0,1;0,0,1|0,0,1;0.5,0,1;0.5,1,1|0,0,1;0.5,1,1;0,1,1|0,1,0;0,1,1;0.5,1,0|0,1,0;0.5,1,0;0.5,0,0|0,1,1;0.5,1,1;0.5,1,0|0.5,0,0;0.5,1,0;0.5,1,1|0.5,0,0;0.5,1,1;0.5,0,1"),
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(result)).ConfigureAwait(false);
return 0;

static Owned<Manifold> CreateBox(
    double minimumX,
    double minimumY,
    double minimumZ,
    double maximumX,
    double maximumY,
    double maximumZ)
{
    double[] vertices =
    [
        minimumX, minimumY, minimumZ,
        maximumX, minimumY, minimumZ,
        maximumX, maximumY, minimumZ,
        minimumX, maximumY, minimumZ,
        minimumX, minimumY, maximumZ,
        maximumX, minimumY, maximumZ,
        maximumX, maximumY, maximumZ,
        minimumX, maximumY, maximumZ,
    ];
    ulong[] triangles =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        3, 7, 6, 3, 6, 2,
        0, 4, 7, 0, 7, 3,
        1, 2, 6, 1, 6, 5,
    ];
    return Manifold.Create(vertices, triangles);
}

static string CanonicalTriangles(ManifoldMeshData mesh)
{
    var triangles = new List<string>(mesh.TriangleIndices.Length / 3);
    for (var index = 0; index < mesh.TriangleIndices.Length; index += 3)
    {
        var points = new string[3];
        for (var point = 0; point < 3; point++)
        {
            var coordinateIndex = checked((int)mesh.TriangleIndices[index + point] * 3);
            points[point] = string.Join(",", new[]
            {
                mesh.VertexCoordinates[coordinateIndex].ToString("R", CultureInfo.InvariantCulture),
                mesh.VertexCoordinates[coordinateIndex + 1].ToString("R", CultureInfo.InvariantCulture),
                mesh.VertexCoordinates[coordinateIndex + 2].ToString("R", CultureInfo.InvariantCulture),
            });
        }

        var rotations = new[]
        {
            string.Join(";", points[0], points[1], points[2]),
            string.Join(";", points[1], points[2], points[0]),
            string.Join(";", points[2], points[0], points[1]),
        };
        triangles.Add(rotations.Min(StringComparer.Ordinal)!);
    }

    triangles.Sort(StringComparer.Ordinal);
    return string.Join("|", triangles);
}

static bool MatchesCanonicalTriangles(ManifoldMeshData mesh, string oracle)
{
    var actualValues = CanonicalTriangles(mesh).Split([';', ',', '|']).Select(Parse).ToArray();
    var oracleValues = oracle.Split([';', ',', '|']).Select(Parse).ToArray();
    return actualValues.Length == oracleValues.Length
        && actualValues.Zip(oracleValues).All(pair => Math.Abs(pair.First - pair.Second) <= 1e-12);

    static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}

static double ContinuousCollision(
    Owned<Manifold> first,
    Owned<Manifold> moving,
    double moveX,
    double moveY,
    double moveZ,
    double tolerance)
{
    if (Intersects(first, moving))
    {
        return 0;
    }

    if (!IntersectsAt(first, moving, moveX, moveY, moveZ, 1))
    {
        return double.NaN;
    }

    var length = Math.Sqrt((moveX * moveX) + (moveY * moveY) + (moveZ * moveZ));
    var low = 0.0;
    var high = 1.0;
    for (var iteration = 0; iteration < 100 && ((high - low) * length) >= tolerance; iteration++)
    {
        var middle = (low + high) / 2;
        if (IntersectsAt(first, moving, moveX, moveY, moveZ, middle))
        {
            high = middle;
        }
        else
        {
            low = middle;
        }
    }

    return high;
}

static bool IntersectsAt(
    Owned<Manifold> first,
    Owned<Manifold> moving,
    double moveX,
    double moveY,
    double moveZ,
    double fraction)
{
    using var translatedValue = moving.Translate(moveX * fraction, moveY * fraction, moveZ * fraction);
    return Intersects(first, translatedValue);
}

static bool Intersects(Owned<Manifold> first, Owned<Manifold> second)
{
    using var intersectionValue = first.Boolean(second, ManifoldOp.Intersect);
    return intersectionValue.NumTri() != 0;
}
