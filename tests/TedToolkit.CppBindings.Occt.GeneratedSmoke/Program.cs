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
if (args is ["--layout-only"])
{
    return;
}

var point = gp_Pnt2dExtensions.Create(1.25, 2.5);
point.SetCoord(3.5, 4.75);
if (point.X() != 3.5 || point.Y() != 4.75)
{
    throw new InvalidOperationException("Value construction or mutation failed.");
}

using var poles = NCollection_Array1_gp_Pnt2dExtensions.Create(1, 2);
var firstPole = gp_Pnt2dExtensions.Create(0, 0);
var secondPole = gp_Pnt2dExtensions.Create(1, 1);
poles.SetValue(1, ref firstPole);
poles.SetValue(2, ref secondPole);

using var knots = NCollection_Array1_doubleExtensions.Create(1, 2);
var firstKnot = 0.0;
var secondKnot = 1.0;
knots.SetValue(1, ref firstKnot);
knots.SetValue(2, ref secondKnot);

using var multiplicities = NCollection_Array1_intExtensions.Create(1, 2);
var multiplicity = 2;
multiplicities.SetValue(1, ref multiplicity);
multiplicities.SetValue(2, ref multiplicity);

using var curve = Geom2d_BSplineCurveExtensions.Create(
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

var color = Quantity_ColorExtensions.Create();
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

using var lineAspect = Prs3d_LineAspectExtensions.Create(in color, Aspect_TypeOfLine.Aspect_TOL_SOLID, 2.5);
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

Console.WriteLine("Generated value, Owned, Handle, borrowed receiver, inheritance, and error smoke passed.");

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