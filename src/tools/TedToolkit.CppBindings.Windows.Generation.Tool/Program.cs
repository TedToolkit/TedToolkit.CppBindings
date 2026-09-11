// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.CppBindings.Windows.Generation.Tool;

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    var options = GenerationCommandLine.Parse(args);
    var coordinator = new WindowsGenerationCoordinator(
        new SystemProcessRunner(),
        new PowerShellNativeDependencyClosure());
    await coordinator.RunAsync(options, CancellationToken.None).ConfigureAwait(false);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}