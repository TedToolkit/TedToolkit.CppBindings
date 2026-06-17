// -----------------------------------------------------------------------
// <copyright file="GenerationOutputCleaner.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Options;

using TedToolkit.Occt.Generator.Options;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

/// <summary>
/// Clears generator output folders while preserving the configured root directories.
/// </summary>
/// <param name="options">The generation options.</param>
public sealed class GenerationOutputCleaner(IOptions<GenerationOptions> options) : IGenerationOutputCleaner
{
    /// <summary>
    /// Empties the configured C# and C++ output folders before generation starts.
    /// </summary>
    public void Clean()
    {
        CleanDirectory(options.Value.CSharpFolder);
        CleanDirectory(options.Value.CppFolder);
    }

    private void CleanDirectory(DirectoryInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!directory.Exists)
        {
            directory.Create();
            return;
        }

        foreach (var fileSystemInfo in directory.GetFileSystemInfos())
        {
            if (fileSystemInfo is DirectoryInfo childDirectory)
            {
                childDirectory.Delete(true);
                continue;
            }

            fileSystemInfo.Delete();
        }
    }
}
