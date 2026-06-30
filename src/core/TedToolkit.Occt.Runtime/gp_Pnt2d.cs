// -----------------------------------------------------------------------
// <copyright file="gp_Pnt2d.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace TedToolkit.Occt;

/// <summary>
/// Represents the native OCCT <c>gp_Pnt2d</c> value.
/// </summary>
#pragma warning disable SA1300
public readonly unsafe struct gp_Pnt2d : IDisposable, IEquatable<gp_Pnt2d>
#pragma warning restore SA1300
{
    /// <summary>
    /// Sets a coordinate value on the native point.
    /// </summary>
    /// <param name="theIndex">The coordinate index.</param>
    /// <param name="theXi">The coordinate value.</param>
    /// <inheritdoc cref="interop_error.ThrowIfError"/>
    public void SetCoord(int theIndex, double theXi)
    {
        fixed (gp_Pnt2d* selfPtr = &this)
        {
            SetCoordNative(selfPtr, theIndex, theXi);
        }
    }

    /// <summary>
    /// Releases the native point storage.
    /// </summary>
    public void Dispose()
    {
        fixed (gp_Pnt2d* selfPtr = &this)
        {
            DeleteNative(selfPtr);
        }
    }

    /// <summary>
    /// Determines whether this value is equal to another point wrapper.
    /// </summary>
    /// <param name="other">The other value to compare.</param>
    /// <returns><see langword="true"/> because the wrapper carries no managed state.</returns>
    public bool Equals(gp_Pnt2d other)
    {
        return true;
    }

    /// <summary>
    /// Determines whether this value is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is a <see cref="gp_Pnt2d"/> value.</returns>
    public override bool Equals(object? obj)
    {
        return obj is gp_Pnt2d;
    }

    /// <summary>
    /// Returns a hash code for this wrapper type.
    /// </summary>
    /// <returns>Always returns <c>0</c> because the wrapper carries no managed state.</returns>
    public override int GetHashCode()
    {
        return 0;
    }

    /// <summary>
    /// Compares two wrapper values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the two wrapper values are equal.</returns>
#pragma warning disable RCS1231
    public static bool operator ==(gp_Pnt2d left, gp_Pnt2d right)
    {
        return left.Equals(right);
    }
#pragma warning restore RCS1231

    /// <summary>
    /// Compares two wrapper values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the two wrapper values are not equal.</returns>
#pragma warning disable RCS1231
    public static bool operator !=(gp_Pnt2d left, gp_Pnt2d right)
    {
        return !left.Equals(right);
    }
#pragma warning restore RCS1231

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("Name", CallingConvention = CallingConvention.Cdecl, EntryPoint = "gp_Pnt2d_SetCoord_int_double")]
    private static extern void SetCoordNative(gp_Pnt2d* self, int theIndex, double theXi);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("Name", CallingConvention = CallingConvention.Cdecl, EntryPoint = "gp_Pnt2d_Delete")]
    private static extern void DeleteNative(gp_Pnt2d* self);
}