// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Runtime.InteropServices;

using TedToolkit.Occt.Runtime.FirstWrapper;
using TedToolkit.Occt.Runtime.SecondWrapper;

namespace TedToolkit.Occt.Runtime.NativeIntegration;

/// <summary>
/// Proves direct Handle release through two independently named C++ fixture libraries.
/// </summary>
internal static unsafe class Program
{
    /// <summary>
    /// Runs the native Handle integration proof.
    /// </summary>
    /// <param name="args">The first and second fixture library paths.</param>
    /// <returns>Zero when both matching native release exports are called exactly once.</returns>
    /// <exception cref="ArgumentException">Exactly two native fixture paths were not supplied.</exception>
    /// <exception cref="InvalidOperationException">
    /// A fixture value was incorrect or its matching release export was not called exactly once.
    /// </exception>
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Expected the first and second native fixture library paths.", nameof(args));
        }

        var firstLibrary = NativeLibrary.Load(args[0]);
        var secondLibrary = NativeLibrary.Load(args[1]);
        var firstReleaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));
        var secondReleaseCount = (int*)NativeMemory.AllocZeroed((nuint)sizeof(int));

        try
        {
            using var first = FirstTransientFactory.Create(firstLibrary, 31, firstReleaseCount);
            using var second = SecondTransientFactory.Create(secondLibrary, 47, secondReleaseCount);

            if (first.Value.Value != 31 || second.Value.Value != 47)
            {
                throw new InvalidOperationException("A native wrapper did not expose its matching exact-layout value.");
            }

            first.Dispose();
            second.Dispose();

            if (*firstReleaseCount != 1 || *secondReleaseCount != 1)
            {
                throw new InvalidOperationException("A matching native release export was not called exactly once.");
            }

            return 0;
        }
        finally
        {
            NativeMemory.Free(firstReleaseCount);
            NativeMemory.Free(secondReleaseCount);
            NativeLibrary.Free(secondLibrary);
            NativeLibrary.Free(firstLibrary);
        }
    }
}