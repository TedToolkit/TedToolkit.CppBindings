// -----------------------------------------------------------------------
// <copyright file="GenerationOutput.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Validates and publishes source paths beneath dedicated, non-overlapping output roots.
/// </summary>
internal static class GenerationOutput
{
    private const int ComparisonBufferSize = 16 * 1024;

    private static readonly SemaphoreSlim ComparisonSlots = new(Math.Max(1, Environment.ProcessorCount));

    private static readonly Regex Identifier = new(@"\A[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly Regex DeviceName = new(
        "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> CSharpKeywords = new(
        ("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double "
         + "else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is "
         + "lock long namespace new null object operator out override params private protected public readonly ref return sbyte "
         + "sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe "
         + "ushort using virtual void volatile while").Split(' '),
        StringComparer.Ordinal);

    /// <summary>
    /// Validates shared target and output settings before provider preparation.
    /// </summary>
    /// <param name="options">The configured shared options.</param>
    /// <exception cref="NotSupportedException">The target is not win-x64.</exception>
    /// <exception cref="ArgumentException">The namespace or output roots are invalid.</exception>
    internal static void ValidateOptions(GenerationOptions options)
    {
        _ = options.GetNativeLibraryBaseName();
        if (options.RuntimeIdentifier != "win-x64")
        {
            throw new NotSupportedException("Only the proved win-x64 generation profile is supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.CSharpNamespace);
        if (options.CSharpNamespace.Split('.').Any(static part => !Identifier.IsMatch(part) || CSharpKeywords.Contains(part)))
        {
            throw new ArgumentException("The generated namespace must contain portable C# identifiers.", nameof(options));
        }

        ArgumentNullException.ThrowIfNull(options.CSharpFolder);
        ArgumentNullException.ThrowIfNull(options.CppFolder);
        var managed = Path.TrimEndingDirectorySeparator(options.CSharpFolder.FullName);
        var native = Path.TrimEndingDirectorySeparator(options.CppFolder.FullName);
        ValidateRoot(managed);
        ValidateRoot(native);
        if (!managed.Equals(native, StringComparison.OrdinalIgnoreCase)
            && !IsBelow(managed, native)
            && !IsBelow(native, managed))
        {
            return;
        }

        throw new ArgumentException("Language output roots must be distinct and non-overlapping.", nameof(options));
    }

    /// <summary>
    /// Rejects invalid paths and collisions before rendering any source.
    /// </summary>
    /// <param name="root">The dedicated language output root.</param>
    /// <param name="sources">The complete source inventory including core support files.</param>
    /// <exception cref="InvalidOperationException">Two output paths collide.</exception>
    internal static void ValidateSources(DirectoryInfo root, IReadOnlyList<GeneratedSource> sources)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(source.RenderAsync);
            var path = Resolve(root, source.RelativePath);
            if (!paths.Add(path))
            {
                throw new InvalidOperationException($"Generated source path collision: '{source.RelativePath}'.");
            }
        }

        foreach (var path in paths)
        {
            for (var parent = Path.GetDirectoryName(path); parent is not null; parent = Path.GetDirectoryName(parent))
            {
                if (paths.Contains(parent))
                {
                    throw new InvalidOperationException($"Generated file/directory collision: '{parent}'.");
                }
            }
        }
    }

    /// <summary>
    /// Checks export identity without changing provider-supplied ordering.
    /// </summary>
    /// <param name="exports">The exact ordered export inventory.</param>
    /// <exception cref="InvalidOperationException">An export is invalid, reserved, or duplicated.</exception>
    internal static void ValidateExports(IReadOnlyList<string> exports)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var export in exports)
        {
            if (export is null || !Identifier.IsMatch(export) || !names.Add(export)
                || export == "NativeApi_GetFunctionTable")
            {
                throw new InvalidOperationException($"Invalid or duplicate native export: '{export}'.");
            }
        }
    }

    /// <summary>
    /// Removes stale output while retaining expected files after rejecting filesystem links.
    /// </summary>
    /// <param name="root">The validated output directory.</param>
    /// <param name="sources">The complete expected source inventory.</param>
    internal static void Reconcile(DirectoryInfo root, IReadOnlyList<GeneratedSource> sources)
    {
        ValidateRoot(root.FullName);
        root.Create();
        foreach (var entry in root.GetFileSystemInfos())
        {
            RejectLinks(entry);
        }

        var expectedFiles = sources
            .Select(source => Resolve(root, source.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in expectedFiles)
        {
            for (var parent = Path.GetDirectoryName(path);
                 parent is not null && IsBelow(parent, Path.TrimEndingDirectorySeparator(root.FullName));
                 parent = Path.GetDirectoryName(parent))
            {
                _ = expectedDirectories.Add(parent);
            }
        }

        ReconcileDirectory(root, expectedFiles, expectedDirectories);
    }

    /// <summary>
    /// Publishes independent sources while preserving provider renderer admission.
    /// </summary>
    /// <param name="root">The validated language output directory.</param>
    /// <param name="sources">The completed source inventory.</param>
    /// <param name="cancellationToken">The pipeline cancellation token.</param>
    /// <returns>The completion of every selected renderer and file write.</returns>
    internal static Task PublishAsync(
        DirectoryInfo root,
        IReadOnlyList<GeneratedSource> sources,
        CancellationToken cancellationToken)
    {
        return Task.WhenAll(sources.Select(source => WriteAsync(root, source, cancellationToken)));
    }

    private static async Task WriteAsync(
        DirectoryInfo root,
        GeneratedSource source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(root, source.RelativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var stagingPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                await using (writer.ConfigureAwait(false))
                {
                    await source.RenderAsync(writer, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path) && await FilesEqualAsync(path, stagingPath, cancellationToken).ConfigureAwait(false))
            {
                File.Delete(stagingPath);
            }
            else
            {
                File.Move(stagingPath, path, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    private static async Task<bool> FilesEqualAsync(
        string firstPath,
        string secondPath,
        CancellationToken cancellationToken)
    {
        await ComparisonSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        byte[]? firstBuffer = null;
        byte[]? secondBuffer = null;
        try
        {
            firstBuffer = ArrayPool<byte>.Shared.Rent(ComparisonBufferSize);
            secondBuffer = ArrayPool<byte>.Shared.Rent(ComparisonBufferSize);
            var first = new FileStream(
                firstPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                ComparisonBufferSize,
                useAsync: true);
            await using (first.ConfigureAwait(false))
            {
                var second = new FileStream(
                    secondPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    ComparisonBufferSize,
                    useAsync: true);
                await using (second.ConfigureAwait(false))
                {
                    if (first.Length != second.Length)
                    {
                        return false;
                    }

                    return await StreamsEqualAsync(
                        first, second, firstBuffer, secondBuffer, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (firstBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(firstBuffer);
            }

            if (secondBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(secondBuffer);
            }

            _ = ComparisonSlots.Release();
        }
    }

    private static async Task<bool> StreamsEqualAsync(
        Stream first,
        Stream second,
        byte[] firstBuffer,
        byte[] secondBuffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var firstRead = await first.ReadAtLeastAsync(
                firstBuffer.AsMemory(0, ComparisonBufferSize),
                ComparisonBufferSize,
                throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);
            var secondRead = await second.ReadAtLeastAsync(
                secondBuffer.AsMemory(0, ComparisonBufferSize),
                ComparisonBufferSize,
                throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);
            if (firstRead != secondRead
                || !firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
            {
                return false;
            }

            if (firstRead == 0)
            {
                return true;
            }
        }
    }

    private static void ReconcileDirectory(
        DirectoryInfo directory,
        IReadOnlySet<string> expectedFiles,
        IReadOnlySet<string> expectedDirectories)
    {
        foreach (var entry in directory.GetFileSystemInfos())
        {
            if (entry is DirectoryInfo child && expectedDirectories.Contains(child.FullName))
            {
                ReconcileDirectory(child, expectedFiles, expectedDirectories);
            }
            else if (!expectedFiles.Contains(entry.FullName))
            {
                if (entry is DirectoryInfo staleDirectory)
                {
                    staleDirectory.Delete(recursive: true);
                }
                else
                {
                    entry.Delete();
                }
            }
            else if (entry is DirectoryInfo fileConflict)
            {
                fileConflict.Delete(recursive: true);
            }
        }
    }

    private static string Resolve(DirectoryInfo root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var segments = relativePath.Replace('\\', '/').Split('/');
        if (Path.IsPathRooted(relativePath) || segments.Any(static segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".."
                || segment.EndsWith(' ') || segment.EndsWith('.')
                || DeviceName.IsMatch(segment)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException($"Invalid output-relative source path: '{relativePath}'.");
        }

        var path = Path.GetFullPath(Path.Combine(root.FullName, Path.Combine(segments)));
        if (!IsBelow(path, Path.TrimEndingDirectorySeparator(root.FullName)))
        {
            throw new InvalidOperationException($"Generated source escapes its output root: '{relativePath}'.");
        }

        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Generated source traverses a filesystem link: '{directory.FullName}'.");
            }
        }

        return path;
    }

    private static bool IsBelow(string path, string root)
    {
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRoot(string path)
    {
        if (Path.GetPathRoot(path) == Path.TrimEndingDirectorySeparator(path)
            || Path.GetPathRoot(path) == path)
        {
            throw new ArgumentException("A filesystem root cannot be used as a generation output directory.", nameof(path));
        }

        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Output roots cannot traverse filesystem links: '{directory.FullName}'.");
            }
        }
    }

    private static void RejectLinks(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Refusing to clean a filesystem link: '{entry.FullName}'.");
        }

        if (entry is not DirectoryInfo directory)
        {
            return;
        }

        foreach (var child in directory.GetFileSystemInfos())
        {
            RejectLinks(child);
        }
    }
}