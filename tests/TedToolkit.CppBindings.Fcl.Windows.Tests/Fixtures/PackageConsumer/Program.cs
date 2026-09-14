using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Fcl;

if (args.Length != 1)
{
    return 2;
}

double[] cubeVertices =
[
    0, 0, 0,
    1, 0, 0,
    1, 1, 0,
    0, 1, 0,
    0, 0, 1,
    1, 0, 1,
    1, 1, 1,
    0, 1, 1,
];
nuint[] cubeTriangles =
[
    0, 2, 1, 0, 3, 2,
    4, 5, 6, 4, 6, 7,
    0, 1, 5, 0, 5, 4,
    3, 7, 6, 3, 6, 2,
    0, 4, 7, 0, 7, 3,
    1, 2, 6, 1, 6, 5,
];
var movingVertices = cubeVertices.Select((value, index) => index % 3 == 0 ? value + 2 : value).ToArray();

var fixedBuild = FclBvhModel.Create(cubeVertices, cubeTriangles);
var movingBuild = FclBvhModel.Create(movingVertices, cubeTriangles);
using var fixedModel = fixedBuild.Model!;
using var movingModel = movingBuild.Model!;
var initial = FclContinuousCollision.Query(fixedModel, fixedModel, new FclVector3(-1, 0, 0));
var preEndpoint = FclContinuousCollision.Query(fixedModel, movingModel, new FclVector3(-1.5, 0, 0));
var endpoint = FclContinuousCollision.Query(fixedModel, movingModel, new FclVector3(-1, 0, 0));
var miss = FclContinuousCollision.Query(fixedModel, movingModel, new FclVector3(1, 0, 0));
var vertexLengthRejected = Rejects<ArgumentException>(() =>
    _ = FclBvhModel.Create(cubeVertices.AsSpan(0, 23), cubeTriangles));
var indexLengthRejected = Rejects<ArgumentException>(() =>
    _ = FclBvhModel.Create(cubeVertices, cubeTriangles.AsSpan(0, 35)));
var badIndices = (nuint[])cubeTriangles.Clone();
badIndices[0] = 99;
var indexRangeRejected = Rejects<ArgumentOutOfRangeException>(() =>
    _ = FclBvhModel.Create(cubeVertices, badIndices));

FclLifetime.Reset();
var emptyBuild = FclBvhModel.Create([], []);
var failedCreateCount = FclLifetime.CreateCount;
var failedDestroyCount = FclLifetime.DestroyCount;

FclLifetime.Reset();
var concurrentBuild = FclBvhModel.Create(cubeVertices, cubeTriangles);
var concurrentOwner = concurrentBuild.Model!;
Parallel.For(0, 32, _ => concurrentOwner.Dispose());
var concurrentCreateCount = FclLifetime.CreateCount;
var concurrentDestroyCount = FclLifetime.DestroyCount;
var laterUseRejected = Rejects<ObjectDisposedException>(() =>
    _ = FclContinuousCollision.Query(concurrentOwner, fixedModel, new FclVector3(0, 0, 0)));

FclLifetime.Reset();
var finalizedOwner = CreateAbandonedModel(cubeVertices, cubeTriangles);
for (var attempt = 0; attempt < 5 && finalizedOwner.IsAlive; attempt++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
var finalizerCreateCount = FclLifetime.CreateCount;
var finalizerDestroyCount = FclLifetime.DestroyCount;

var result = new
{
    FixedCode = fixedBuild.Code.ToString(),
    MovingCode = movingBuild.Code.ToString(),
    Initial = initial,
    PreEndpoint = preEndpoint,
    Endpoint = endpoint,
    Miss = miss,
    VertexLengthRejected = vertexLengthRejected,
    IndexLengthRejected = indexLengthRejected,
    IndexRangeRejected = indexRangeRejected,
    EmptyCode = emptyBuild.Code.ToString(),
    EmptyHasNoOwner = emptyBuild.Model is null,
    FailedCreateCount = failedCreateCount,
    FailedDestroyCount = failedDestroyCount,
    ConcurrentCreateCount = concurrentCreateCount,
    ConcurrentDestroyCount = concurrentDestroyCount,
    LaterUseRejected = laterUseRejected,
    FinalizedOwnerReleased = !finalizedOwner.IsAlive,
    FinalizerCreateCount = finalizerCreateCount,
    FinalizerDestroyCount = finalizerDestroyCount,
};
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(result)).ConfigureAwait(false);
return 0;

static bool Rejects<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
        return false;
    }
    catch (TException)
    {
        return true;
    }
}

[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference CreateAbandonedModel(double[] vertices, nuint[] triangles)
{
    var build = FclBvhModel.Create(vertices, triangles);
    return new WeakReference(build.Model!);
}

static unsafe class FclLifetime
{
    private static readonly nint* Functions;

    static FclLifetime()
    {
        var module = NativeLibrary.Load("ted_toolkit_cpp_bindings_fcl",
            typeof(FclBvhModel).Assembly, null);
        Functions = ((delegate* unmanaged[Cdecl]<nint*>)NativeLibrary.GetExport(
            module, "NativeApi_GetFunctionTable"))();
    }

    internal static int CreateCount => ((delegate* unmanaged[Cdecl]<int>)Functions[5])();

    internal static int DestroyCount => ((delegate* unmanaged[Cdecl]<int>)Functions[6])();

    internal static void Reset()
    {
        ((delegate* unmanaged[Cdecl]<void>)Functions[4])();
    }
}
