// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.CppBindings;
using TedToolkit.CppBindings.Occt;

GeneratedLayoutProbe.Run();
PreparedLayoutProbe.Run();
GeneratedCycleProbe.Run();
if (args is ["--layout-only"])
{
    return;
}

var point = gp_Pnt2d.Create(1.25, 2.5);
point.SetCoord(3.5, 4.75);
if (point.X() != 3.5 || point.Y() != 4.75)
{
    throw new InvalidOperationException("Value construction or mutation failed.");
}

using var poles = NCollection_Array1_gp_Pnt2d.Create(1, 2);
var firstPole = gp_Pnt2d.Create(0, 0);
var secondPole = gp_Pnt2d.Create(1, 1);
poles.SetValue(1, ref firstPole);
poles.SetValue(2, ref secondPole);

using var knots = NCollection_Array1_double.Create(1, 2);
var firstKnot = 0.0;
var secondKnot = 1.0;
knots.SetValue(1, ref firstKnot);
knots.SetValue(2, ref secondKnot);

using var multiplicities = NCollection_Array1_int.Create(1, 2);
var multiplicity = 2;
multiplicities.SetValue(1, ref multiplicity);
multiplicities.SetValue(2, ref multiplicity);

using var curve = Geom2d_BSplineCurve.Create(
    poles,
    knots,
    multiplicities,
    1,
    false);
if (curve.FirstParameter() != 0 || curve.LastParameter() != 1)
{
    throw new InvalidOperationException("Inherited transient operation failed.");
}

var projected = false;
try
{
    _ = point.Coord(3);
}
catch (OcctException)
{
    projected = true;
}

if (!projected)
{
    throw new InvalidOperationException("Native OCCT failure was not projected.");
}

var color = Quantity_Color.Create();
var frustum = Aspect_FrustumLRBT_doubleExtensions.Create();
frustum.Left = 1;
frustum.Right = 2;
frustum.Bottom = 3;
frustum.Top = 4;
frustum.Multiply(2);
var scaledFrustum = frustum.Multiplied(3);
if (System.Runtime.CompilerServices.Unsafe.SizeOf<Aspect_FrustumLRBT<double>>() != 32
    || frustum.Left != 2 || frustum.Right != 4 || frustum.Bottom != 6 || frustum.Top != 8
    || scaledFrustum.Left != 6 || scaledFrustum.Right != 12 || scaledFrustum.Bottom != 18 || scaledFrustum.Top != 24)
{
    throw new InvalidOperationException("Generic template layout or closed native specialization dispatch failed.");
}

using var lineAspect = Prs3d_LineAspect.Create(in color, Aspect_TypeOfLine.Aspect_TOL_SOLID, 2.5);
ref readonly var borrowedAspect = ref Prs3d_LineAspectExtensions.Aspect(lineAspect);
if (Graphic3d_AspectLine3dExtensions.Width(in borrowedAspect) != 2.5f)
{
    throw new InvalidOperationException("Borrowed native handle receiver failed.");
}

Prs3d_LineAspectExtensions.SetWidth(lineAspect, 3.5);
if (Graphic3d_AspectLine3dExtensions.Width(in borrowedAspect) != 3.5f)
{
    throw new InvalidOperationException("Borrowed handle did not observe the native owner's mutation.");
}

lineAspect.Dispose();
var rejectedDisposedOwner = false;
try
{
    _ = Prs3d_LineAspectExtensions.Aspect(lineAspect);
}
catch (ObjectDisposedException)
{
    rejectedDisposedOwner = true;
}

if (!rejectedDisposedOwner)
{
    throw new InvalidOperationException("Owning receiver accepted a disposed native owner.");
}

MemoryBrepProbe.Run();
Console.WriteLine("Generated value, Owned, Handle, borrowed receiver, inheritance, and error smoke passed.");

internal static class MemoryBrepProbe
{
    /// <summary>
    /// Verifies that native stream bindings preserve a vertex through an in-memory BREP round-trip.
    /// </summary>
    public static void Run()
    {
        var builder = BRep_Builder.Create();
        var point = gp_Pnt.Create(1.25, -2.5, 3.75);
        using var vertex = TopoDS_Vertex.Create();
        builder.MakeVertex(vertex, in point, 1e-7);
        using var identity = TopLoc_Location.Create();
        using var shape = vertex.Moved(identity, false);
        if (shape.IsNull())
        {
            throw new InvalidOperationException("A constructed vertex was reported as null.");
        }

        var streams = CreateOutput();
        using var buffer = streams.Buffer;
        using var output = streams.Output;
        var bufferAddress = Address(buffer);
        var outputAddress = Address(output);
        using var progress = Message_ProgressRange.Create();
        CollectAndChurn();
        if (Address(buffer) != bufferAddress || Address(output) != outputAddress)
        {
            throw new InvalidOperationException("Native stream storage moved after construction.");
        }

        BRepToolsExtensions.Write(shape, output, progress);
        using var text = buffer.str();
        var bytes = new byte[checked((int)text.size())];
        if (output.fail() || bytes.Length == 0
            || text.copy(ref MemoryMarshal.Cast<byte, sbyte>(bytes.AsSpan())[0], (ulong)bytes.Length, 0) != (ulong)bytes.Length
            || bytes.Contains((byte)0)
            || !System.Text.Encoding.ASCII.GetString(bytes).Contains("CASCADE Topology", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Native BREP output did not produce complete, unterminated text bytes.");
        }

        using var input = std_basic_istream_char.Create(buffer);
        var inputAddress = Address(input);
        using var restored = TopoDS_Shape.Create();
        CollectAndChurn();
        if (Address(buffer) != bufferAddress || Address(input) != inputAddress)
        {
            throw new InvalidOperationException("Native input storage moved after construction.");
        }

        BRepToolsExtensions.Read(restored, input, in builder, progress);
        if (restored.IsNull() || restored.ShapeType() != TopAbs_ShapeEnum.TopAbs_VERTEX)
        {
            throw new InvalidOperationException("Memory BREP input did not restore the vertex topology.");
        }

        var bounds = Bnd_Box.Create();
        BRepBndLibExtensions.Add(restored, ref bounds, false);
        var limits = bounds.Get();
        if (!(Math.Abs(((limits.Xmin + limits.Xmax) / 2) - 1.25) < 1e-10)
            || !(Math.Abs(((limits.Ymin + limits.Ymax) / 2) + 2.5) < 1e-10)
            || !(Math.Abs(((limits.Zmin + limits.Zmax) / 2) - 3.75) < 1e-10))
        {
            throw new InvalidOperationException("Memory BREP round-trip changed the vertex coordinates.");
        }

        Console.WriteLine("Memory BREP round-trip preserved vertex topology and coordinates across compacting GC.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Owned<std_basic_stringbuf_char> Buffer, Owned<std_basic_ostream_char> Output) CreateOutput()
    {
        var buffer = std_basic_stringbuf_char.Create();
        try
        {
            return (buffer, std_basic_ostream_char.Create(buffer));
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe nint Address<T>(Owned<T> owner)
        where T : unmanaged, ICppRaii
    {
        fixed (T* pointer = &owner.Value)
        {
            return (nint)pointer;
        }
    }

    private static void CollectAndChurn()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        var blocks = new byte[2048][];
        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = new byte[4096];
            blocks[index].AsSpan().Fill(0xcc);
        }

        GC.KeepAlive(blocks);
    }
}

internal static class GeneratedLayoutProbe
{
    public static void Run()
    {
        var check = typeof(GeneratedLayoutProbe).GetMethod(nameof(Check))!;
        var count = 0;
        foreach (var type in typeof(gp_Pnt2d).Assembly.GetTypes().Where(static type =>
                     type.IsValueType && !type.IsEnum && !type.ContainsGenericParameters
                     && type.IsDefined(typeof(NativeTypeNameAttribute), inherit: false)))
        {
            var layout = type.StructLayoutAttribute!;
            if (layout.Value != LayoutKind.Sequential || layout.Size <= 0 || layout.Pack <= 0)
            {
                throw new InvalidOperationException($"Missing sequential native layout facts: {type}.");
            }

            check.MakeGenericMethod(type).Invoke(null, [layout.Size, layout.Pack]);
            count++;
        }

        if (count == 0)
        {
            throw new InvalidOperationException("No generated closed layouts were checked.");
        }

        Check<OpenGl_SetOfPrograms>(131088, 8);
        Console.WriteLine($"Loaded and checked {count} generated closed layouts, including large opaque storage.");
    }

    public static void Check<T>(int size, int alignment)
        where T : unmanaged
    {
        Holder<T> holder = default;
        var offset = Unsafe.ByteOffset(ref holder.Prefix, ref Unsafe.As<T, byte>(ref holder.Value));
        if (Unsafe.SizeOf<T>() != size || offset != alignment)
        {
            throw new InvalidOperationException(
                $"Managed/native layout mismatch for {typeof(T)}: size {Unsafe.SizeOf<T>()}/{size}, alignment {offset}/{alignment}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Holder<T>
        where T : unmanaged
    {
        public byte Prefix;
        public T Value;
    }
}

internal static class GeneratedCycleProbe
{
    public static unsafe void Run()
    {
        MAT_Edge native = default;
        var moved = false;
        for (var attempt = 0; attempt < 8 && !moved; attempt++)
        {
            var heap = new Heap();
            ref var view = ref heap.Value.thefirstedge;
            Unsafe.As<handle<MAT_Edge>, nint>(ref view) = (nint)(&native);
            var before = Address(ref view);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            moved = before != Address(ref view);
            if (!Unsafe.AreSame(ref view, ref heap.Value.thefirstedge))
            {
                throw new InvalidOperationException("Cyclic handle reference stopped aliasing its original field.");
            }

            view.Value.theedgenumber = 73;
            if (native.theedgenumber != 73)
            {
                throw new InvalidOperationException("Cyclic handle reference copied the native target.");
            }

            heap.Value.thefirstedge = default;
            if (Unsafe.As<handle<MAT_Edge>, nint>(ref view) != 0)
            {
                throw new InvalidOperationException("Whole-handle assignment did not update the original slot.");
            }
        }

        if (!moved)
        {
            throw new InvalidOperationException("No heap relocation was observed; cyclic reference proof is incomplete.");
        }

        Console.WriteLine("Cyclic handle aliases survived observed GC relocation, target mutation and whole-slot assignment.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe nint Address(ref handle<MAT_Edge> value)
    {
        fixed (handle<MAT_Edge>* pointer = &value)
        {
            return (nint)pointer;
        }
    }

    private sealed class Heap
    {
        public MAT_Bisector Value = default;
    }
}
