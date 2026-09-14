// -----------------------------------------------------------------------
// <copyright file="SecondRaiiFactory.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TedToolkit.CppBindings;

namespace TedToolkit.CppBindings.Runtime.SecondWrapper;

/// <summary>
/// Provides generated-shape construction for the second independent RAII wrapper fixture.
/// </summary>
public static unsafe class SecondRaiiFactory
{
    /// <summary>
    /// Creates one owned RAII fixture directly in the Runtime owner's managed storage.
    /// </summary>
    /// <param name="value">The initial fixture value.</param>
    /// <param name="destroyCount">The unmanaged counter incremented by destruction.</param>
    /// <returns>The constructed owner.</returns>
    [SuppressMessage(
        "Usage",
        "CA1816:Dispose methods should call SuppressFinalize",
        Justification = "Generated construction must suppress the unreturned owner when native construction fails.")]
    public static Owned<SecondRaii> Create(int value, int* destroyCount)
    {
        ArgumentNullException.ThrowIfNull(destroyCount);

        var owner = new Owned<SecondRaii>(&Destroy);
        var constructionSucceeded = false;

        try
        {
            fixed (SecondRaii* target = &owner.Value)
            {
                target->Value = value;
                target->DestroyCount = destroyCount;
            }

            constructionSucceeded = true;
            GC.KeepAlive(owner);
            return owner;
        }
        finally
        {
            if (!constructionSucceeded)
            {
                GC.SuppressFinalize(owner);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl),])]
    private static void Destroy(SecondRaii* target)
    {
        Interlocked.Increment(ref *target->DestroyCount);
    }
}