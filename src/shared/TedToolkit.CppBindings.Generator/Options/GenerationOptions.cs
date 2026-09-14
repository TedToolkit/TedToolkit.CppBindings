// -----------------------------------------------------------------------
// <copyright file="GenerationOptions.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.CppBindings.Generator;

/// <summary>
/// Configures provider-neutral source publication and the supported Windows target.
/// </summary>
public record GenerationOptions
{
    /// <summary>
    /// The default native library artifact basename.
    /// </summary>
    internal const string DefaultNativeLibraryBaseName = "ted_toolkit_cpp_bindings";

    /// <summary>
    /// Gets the output folder for generated C# sources.
    /// </summary>
    public required DirectoryInfo CSharpFolder { get; init; }

    /// <summary>
    /// Gets the output folder for generated C++ sources.
    /// </summary>
    public required DirectoryInfo CppFolder { get; init; }

    /// <summary>
    /// Gets the portable basename for the generated native library artifact.
    /// </summary>
    /// <remarks>
    /// The default is <c>ted_toolkit_cpp_bindings</c>.
    /// The build system supplies the platform prefix and extension. The value must contain 1 through 240 ASCII
    /// letters, digits, underscores, dots, or hyphens; begin with a letter or underscore; and end with a letter,
    /// digit, or underscore. Platform prefixes, suffixes, paths, and Windows reserved device names are rejected.
    /// </remarks>
    public string NativeLibraryBaseName { get; init; } = DefaultNativeLibraryBaseName;

    /// <summary>
    /// Gets a value indicating whether generated types should be internal.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// Gets the namespace shared by generated managed sources and the native loader.
    /// </summary>
    public string CSharpNamespace { get; init; } = "TedToolkit.CppBindings";

    /// <summary>
    /// Gets the supported Windows x64 target identifier; other targets fail before preparation.
    /// </summary>
    public string RuntimeIdentifier { get; init; } = "win-x64";

    /// <summary>
    /// Gets or sets the C++ language standard version passed to clang and CMake.
    /// </summary>
    public int CppVersion { get; set; } = 17;

    /// <summary>
    /// Gets the validated native library artifact basename.
    /// </summary>
    /// <returns>The validated basename.</returns>
    /// <exception cref="ArgumentException">The configured basename is not portable or safe.</exception>
    internal string GetNativeLibraryBaseName()
    {
        return ValidateNativeLibraryBaseName(NativeLibraryBaseName);
    }

    /// <summary>
    /// Validates a native library artifact basename.
    /// </summary>
    /// <param name="nativeLibraryBaseName">The basename to validate.</param>
    /// <returns>The validated basename.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nativeLibraryBaseName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="nativeLibraryBaseName"/> is not portable or safe.</exception>
    internal static string ValidateNativeLibraryBaseName(string nativeLibraryBaseName)
    {
        ArgumentNullException.ThrowIfNull(nativeLibraryBaseName);

        if (nativeLibraryBaseName.Length is < 1 or > 240
            || !IsAsciiLetterOrUnderscore(nativeLibraryBaseName[0])
            || !IsAsciiLetterDigitOrUnderscore(nativeLibraryBaseName[^1])
            || nativeLibraryBaseName.Any(character => !IsPortableBaseNameCharacter(character))
            || nativeLibraryBaseName.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
            || HasPlatformLibrarySuffix(nativeLibraryBaseName)
            || IsWindowsReservedDeviceName(nativeLibraryBaseName))
        {
            throw new ArgumentException(
                "The native library basename must be a portable unprefixed name without a platform extension.",
                nameof(nativeLibraryBaseName));
        }

        return nativeLibraryBaseName;
    }

    private static bool IsAsciiLetterOrUnderscore(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';
    }

    private static bool IsAsciiLetterDigitOrUnderscore(char character)
    {
        return IsAsciiLetterOrUnderscore(character) || character is >= '0' and <= '9';
    }

    private static bool IsPortableBaseNameCharacter(char character)
    {
        return IsAsciiLetterDigitOrUnderscore(character) || character is '.' or '-';
    }

    private static bool HasPlatformLibrarySuffix(string nativeLibraryBaseName)
    {
        return nativeLibraryBaseName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
               || nativeLibraryBaseName.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
               || nativeLibraryBaseName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWindowsReservedDeviceName(string nativeLibraryBaseName)
    {
        var separatorIndex = nativeLibraryBaseName.IndexOf('.', StringComparison.Ordinal);
        var stem = separatorIndex < 0 ? nativeLibraryBaseName : nativeLibraryBaseName[..separatorIndex];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
               && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                   || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
               && stem[3] is >= '1' and <= '9';
    }
}