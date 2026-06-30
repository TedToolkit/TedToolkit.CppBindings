// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncTest.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using ModularPipelines.Context;

using TedToolkit.Occt.Generator.Modules;
using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Tests.Modules.CleanGenerationOutputModuleTests;

/// <summary>
/// <see cref="CleanGenerationOutputModule"/> execution.
/// </summary>
internal sealed class ExecuteAsyncTest
{
    /// <summary>
    /// Verifies the module delegates cleanup work to the injected cleaner.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_invoke_cleaner_once_through_the_injected_dependency_Async()
    {
        var cleaner = IGenerationOutputCleaner.Mock();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IGenerationOutputCleaner>(cleaner)
            .AddSingleton<CleanGenerationOutputModule>()
            .BuildServiceProvider();
        await using var _ = serviceProvider.ConfigureAwait(false);

        var module = serviceProvider.GetRequiredService<CleanGenerationOutputModule>();
        var context = Mock.Of<IModuleContext>();

        var executeAsyncMethod = typeof(CleanGenerationOutputModule).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        await Assert.That(executeAsyncMethod).IsNotNull();

        var task = (Task<bool>)executeAsyncMethod!
            .Invoke(module, [context, CancellationToken.None,])!;

        var result = await task.ConfigureAwait(false);

        await Assert.That(result).IsTrue();
        cleaner.Clean().WasCalled(Times.Once);
    }
}