// -----------------------------------------------------------------------
// <copyright file="LoadTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

using TedToolkit.FileSystem;

namespace TedToolkit.CppBindings.Cgal.Generator.Tests.CgalProfileManifestTests;

/// <summary>
/// Verifies explicit profile loading through typed file paths.
/// </summary>
internal sealed class LoadTests
{
    /// <summary>
    /// Verifies an explicit profile is read from a FilePath without requiring installed native libraries.
    /// </summary>
    /// <returns>A task that completes when profile assertions finish.</returns>
    [Test]
    public async Task Should_load_an_explicit_profile_from_a_typed_file_path_Async()
    {
        var root = DirectoryPath.CreateTempSubdirectory("CppBindings.Cgal.FileSystem.");
        try
        {
            var profile = CgalProfileManifest.LoadDefault() with { ProfileId = "typed-path-profile", };
            var file = root / new FileName("profile.json");
            await file.WriteAllTextAsync(JsonSerializer.Serialize(profile)).ConfigureAwait(false);

            var loaded = CgalProfileManifest.Load(file);

            await Assert.That(loaded.ProfileId).IsEqualTo("typed-path-profile");
            await Assert.That(loaded.SelectedHeaders).IsEquivalentTo(profile.SelectedHeaders);
            await Assert.That(loaded.Declarations.Select(static item => item.Id))
                .IsEquivalentTo(profile.Declarations.Select(static item => item.Id));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies an uninitialized file path is rejected before accessing the filesystem.
    /// </summary>
    /// <returns>A task that completes when rejection assertions finish.</returns>
    [Test]
    public async Task Should_reject_an_uninitialized_file_path_Async()
    {
        await Assert.That(() => CgalProfileManifest.Load(default)).Throws<ArgumentNullException>();
    }
}