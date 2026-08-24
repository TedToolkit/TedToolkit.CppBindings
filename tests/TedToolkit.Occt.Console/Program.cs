// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using ModularPipelines;

using Sourcy.DotNet;

using TedToolkit.Occt.Generator;
using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Options;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 2 && string.Equals(args[0], "--materialize-abi-v1", StringComparison.Ordinal))
{
    _ = await GenerateCppModule.GenerateAbiProjectAsync(new(args[1]), CancellationToken.None)
        .ConfigureAwait(false);
    return;
}

if (args.Length != 0)
{
    throw new ArgumentException("Expected no arguments or --materialize-abi-v1 <output-directory>.", nameof(args));
}

var outputFolder = Solutions.TedToolkit_Occt.Directory
                       ?.CreateSubdirectory("output")
                       .CreateSubdirectory("generated")
                   ?? throw new InvalidOperationException("Output folder not found");

var pipeline = await Pipeline.CreateBuilder()
    .AddOcctGenerators(
        new GenerationOptions()
        {
            DeclOptions =
            [
                new("Geom2d_BSplineCurve"),
            ],
            CSharpFolder = outputFolder.CreateSubdirectory("csharp"),
            CppFolder = outputFolder.CreateSubdirectory("cpp"),
            CommandLineArgs = [],
            FieldTypeToGenerate = field =>
            {
                var type = field.Type.CanonicalType;
                return !type.AsString.Contains("std::", StringComparison.Ordinal);
            },
        }).BuildAsync().ConfigureAwait(false);

await pipeline
    .RunAsync().ConfigureAwait(false);