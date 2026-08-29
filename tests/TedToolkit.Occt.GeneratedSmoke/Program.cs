// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt;

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

Console.WriteLine("Generated value, Owned, Handle, inheritance, and error smoke passed.");
