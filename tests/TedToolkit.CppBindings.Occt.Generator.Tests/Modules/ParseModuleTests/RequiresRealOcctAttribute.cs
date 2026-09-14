// -----------------------------------------------------------------------
// <copyright file="RequiresRealOcctAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Occt.Generator.Tests.Modules.ParseModuleTests;

using TedToolkit.CppBindings.Occt.Generator.Services;

/// <summary>
/// Skips the real OCCT boundary test when its approved local fixture is unavailable.
/// </summary>
internal sealed class RequiresRealOcctAttribute : TUnit.Core.SkipAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresRealOcctAttribute"/> class.
    /// </summary>
    public RequiresRealOcctAttribute()
        : base("Requires VCPKG_ROOT with an installed OCCT triplet containing Geom2d_BSplineCurve.hxx.")
    {
    }

    /// <inheritdoc />
    public override Task<bool> ShouldSkip(TUnit.Core.TestRegisteredContext context)
    {
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (string.IsNullOrWhiteSpace(vcpkgRoot))
        {
            return Task.FromResult(true);
        }

        try
        {
            var triplet = new VcpkgDefaultTripletResolver().GetTriplet();
            var targetHeader = Path.Combine(
                vcpkgRoot,
                "installed",
                triplet,
                "include",
                "opencascade",
                "Geom2d_BSplineCurve.hxx");
            return Task.FromResult(!File.Exists(targetHeader));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(true);
        }
    }
}