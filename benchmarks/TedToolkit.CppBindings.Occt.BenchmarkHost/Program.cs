// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using ModularPipelines;

using Sourcy.DotNet;

using TedToolkit.CppBindings.Occt.Generator;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--output-root"))
{
    throw new ArgumentException("Expected no arguments or --output-root <directory>.", nameof(args));
}

var rootSolution = typeof(Solutions)
                       .GetProperties()
                       .Select(static property => property.GetValue(null))
                       .OfType<FileInfo>()
                       .Where(static solution => string.Equals(
                           solution.Name,
                           "TedToolkit.CppBindings.slnx",
                           StringComparison.Ordinal))
                       .MinBy(static solution => solution.FullName.Length)
                   ?? throw new InvalidOperationException("Root solution not found");

var outputFolder = args.Length is 2
    ? Directory.CreateDirectory(Path.GetFullPath(args[1]))
    : rootSolution.Directory?.CreateSubdirectory("output").CreateSubdirectory("generated")
      ?? throw new InvalidOperationException("Output folder not found");

var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(
        new OcctGenerationOptions()
        {
            DeclOptions = [],
            GenerateAllPublicHeaders = true,
            CSharpFolder = outputFolder.CreateSubdirectory("csharp"),
            CppFolder = outputFolder.CreateSubdirectory("cpp"),
            CommandLineArgs = ["-w",],
        }).BuildAsync().ConfigureAwait(false);

await pipeline.RunAsync().ConfigureAwait(false);