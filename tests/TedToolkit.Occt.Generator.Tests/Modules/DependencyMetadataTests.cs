// -----------------------------------------------------------------------
// <copyright file="DependencyMetadataTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Occt.Generator.Modules;

namespace TedToolkit.Occt.Generator.Tests.Modules;

/// <summary>
/// Module dependency metadata behavior.
/// </summary>
internal sealed class DependencyMetadataTests
{
    /// <summary>
    /// Verifies Clean and Parse are independent prerequisites of both generators.
    /// </summary>
    /// <returns>A task that completes when the assertion sequence has finished.</returns>
    [Test]
    public async Task Should_require_clean_and_parse_before_both_generators_Async()
    {
        Type[] expectedDependencies = [typeof(CleanGenerationOutputModule), typeof(ParseModule),];

        await Assert.That(GetDependencies(typeof(GenerateCSharpModule))).IsEquivalentTo(expectedDependencies);
        await Assert.That(GetDependencies(typeof(GenerateCppModule))).IsEquivalentTo(expectedDependencies);
        await Assert.That(GetDependencies(typeof(CleanGenerationOutputModule))).IsEmpty();
        await Assert.That(GetDependencies(typeof(ParseModule))).IsEmpty();
    }

    private static Type[] GetDependencies(Type moduleType)
    {
        return moduleType.GetCustomAttributesData()
            .Where(static attribute => attribute.AttributeType.IsGenericType)
            .Select(static attribute => attribute.AttributeType.GetGenericArguments()[0])
            .ToArray();
    }
}