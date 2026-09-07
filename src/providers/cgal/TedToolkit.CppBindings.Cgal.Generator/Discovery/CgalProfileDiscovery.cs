// -----------------------------------------------------------------------
// <copyright file="CgalProfileDiscovery.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TedToolkit.CppBindings.Cgal.Generator;

/// <summary>
/// Resolves real header reachability, declaration evidence, and installed toolchain identity.
/// </summary>
internal static partial class CgalProfileDiscovery
{
    /// <summary>
    /// Discovers the finite profile against installed headers and toolchain state.
    /// </summary>
    /// <param name="options">The generation options.</param>
    /// <param name="profile">The selected immutable profile.</param>
    /// <returns>The deterministic discovered inventory.</returns>
    /// <exception cref="DirectoryNotFoundException">The installed CGAL include tree is absent.</exception>
    /// <exception cref="InvalidOperationException">The profile, installed headers, or toolchain is inconsistent.</exception>
    internal static CgalGenerationInventory Resolve(CgalGenerationOptions options, CgalProfileManifest profile)
    {
        ValidateProfile(profile);
        var includeRoot = Path.Combine(options.VcpkgRoot.FullName, "installed", profile.Triplet, "include");
        var cgalRoot = Path.Combine(includeRoot, "CGAL");
        if (!Directory.Exists(cgalRoot))
        {
            throw new DirectoryNotFoundException($"CGAL public headers were not found beneath '{cgalRoot}'.");
        }

        var installed = Directory.EnumerateFiles(cgalRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(includeRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var locked = CgalProfileResources.LoadLockedHeaders();
        if (options.RequireLockedHeaderInventory && !installed.SequenceEqual(locked, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The installed CGAL public-header inventory does not match the locked {profile.CgalVersion} profile.");
        }

        var reachable = CloseHeaderIncludes(includeRoot, profile.SelectedHeaders);
        var sourceText = string.Join(
            "\n",
            reachable.Order(StringComparer.Ordinal).Select(path => File.ReadAllText(Path.Combine(
                includeRoot,
                path.Replace('/', Path.DirectorySeparatorChar)))));
        var declarationCatalog = profile.Declarations.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        foreach (var declaration in profile.Declarations)
        {
            foreach (var dependency in declaration.Dependencies)
            {
                if (!declarationCatalog.ContainsKey(dependency))
                {
                    throw new InvalidOperationException(
                        $"Profile declaration '{declaration.Id}' requires missing declaration '{dependency}'.");
                }
            }
        }

        var candidates = profile.Declarations.OrderBy(static item => item.Id, StringComparer.Ordinal)
            .Select(item => Classify(item, reachable, sourceText))
            .ToArray();
        var admitted = candidates.Where(static item => item.Disposition == "admitted").ToArray();
        var unsupported = candidates.Where(static item => item.Disposition == "unsupported").ToArray();
        var toolchain = ResolveToolchain(options.VcpkgRoot, profile.Triplet);
        if (options.RequireLockedToolchain)
        {
            ValidateToolchain(profile, toolchain);
        }

        var roots = profile.SelectedHeaders.ToHashSet(StringComparer.Ordinal);
        return new()
        {
            Sources = Array.AsReadOnly(installed.Select(header => new CgalSourceDisposition(
                header,
                GetSourceDisposition(header, roots, reachable)))
                .ToArray()),
            Candidates = Array.AsReadOnly(candidates),
            Admitted = Array.AsReadOnly(admitted),
            Unsupported = Array.AsReadOnly(unsupported),
            ManagedArtifacts = CgalSemanticCatalog.CreateManagedArtifacts(admitted),
            NativeArtifacts = CgalSemanticCatalog.CreateNativeArtifacts(admitted),
            Toolchain = toolchain,
        };
    }

    private static string GetSourceDisposition(
        string header,
        HashSet<string> roots,
        HashSet<string> reachable)
    {
        if (roots.Contains(header))
        {
            return "profile-root";
        }

        return reachable.Contains(header) ? "reachable-dependency" : "not-reachable-from-finite-profile";
    }

    private static void ValidateProfile(CgalProfileManifest profile)
    {
        if (profile.SchemaVersion != 1)
        {
            throw new NotSupportedException($"Unsupported CGAL profile schema {profile.SchemaVersion}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.CgalVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.VcpkgBuiltinBaseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Triplet);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.KernelAlias);
        if (profile.SelectedHeaders.Count == 0 || profile.Declarations.Count == 0)
        {
            throw new InvalidOperationException("A finite CGAL profile requires headers and declarations.");
        }

        if (profile.SelectedHeaders.Distinct(StringComparer.Ordinal).Count() != profile.SelectedHeaders.Count
            || profile.Declarations.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count()
            != profile.Declarations.Count)
        {
            throw new InvalidOperationException("CGAL profile header and declaration identities must be unique.");
        }

        foreach (var declaration in profile.Declarations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.NativeSignature);
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Header);
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Kind);
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Evidence);
        }
    }

    private static HashSet<string> CloseHeaderIncludes(string includeRoot, IReadOnlyList<string> roots)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(roots.Order(StringComparer.Ordinal));
        while (pending.TryDequeue(out var header))
        {
            var normalized = header.Replace('\\', '/');
            if (!closure.Add(normalized))
            {
                continue;
            }

            var path = Path.Combine(includeRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Profile header '{normalized}' is absent from the installed package.");
            }

            foreach (Match match in CgalInclude().Matches(File.ReadAllText(path)))
            {
                var dependency = match.Groups["path"].Value.Replace('\\', '/');
                if (File.Exists(Path.Combine(includeRoot, dependency.Replace('/', Path.DirectorySeparatorChar))))
                {
                    pending.Enqueue(dependency);
                }
            }
        }

        return closure;
    }

    private static CgalDeclarationDisposition Classify(
        CgalProfileDeclaration declaration,
        HashSet<string> reachableHeaders,
        string reachableSource)
    {
        if (!reachableHeaders.Contains(declaration.Header))
        {
            return Result(declaration, "unsupported", "declaring-header-not-reachable");
        }

        if (!reachableSource.Contains(declaration.Evidence, StringComparison.Ordinal))
        {
            return Result(declaration, "unsupported", $"source-evidence-not-found:{declaration.Evidence}");
        }

        return CgalSemanticCatalog.IsSupported(declaration.Id)
            ? Result(declaration, "admitted", "source-evidence-and-provider-semantic-projection-proved")
            : Result(declaration, "unsupported", "no-provider-semantic-projection");
    }

    private static CgalDeclarationDisposition Result(
        CgalProfileDeclaration declaration,
        string disposition,
        string proof)
    {
        return new(
            declaration.Id,
            declaration.NativeSignature,
            declaration.Header,
            declaration.Kind,
            disposition,
            proof);
    }

    private static CgalResolvedToolchain ResolveToolchain(DirectoryInfo vcpkgRoot, string triplet)
    {
        var statusPath = Path.Combine(vcpkgRoot.FullName, "installed", "vcpkg", "status");
        var status = File.ReadAllText(statusPath);
        var cgal = ReadPackage(status, "cgal", triplet);
        var gmp = ReadPackage(status, "gmp", triplet);
        var mpfr = ReadPackage(status, "mpfr", triplet);
        return new(
            cgal.Version,
            cgal.Abi,
            gmp.Version,
            gmp.Abi,
            mpfr.Version,
            mpfr.Abi,
            ReadCMakeVersion(),
            ReadMsvcVersion());
    }

    private static (string Version, string Abi) ReadPackage(string status, string package, string triplet)
    {
        var match = Regex.Match(
            status,
            $"(?ms)^Package: {Regex.Escape(package)}\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\z)",
            RegexOptions.CultureInvariant);
        if (!match.Success || !match.Groups["body"].Value.Contains($"Architecture: {triplet}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Installed vcpkg package '{package}:{triplet}' was not found.");
        }

        var body = match.Groups["body"].Value;
        var version = ReadStatusValue(body, "Version");
        var portVersion = TryReadStatusValue(body, "Port-Version");
        if (!string.IsNullOrEmpty(portVersion))
        {
            version += "#" + portVersion;
        }

        return (version, ReadStatusValue(body, "Abi"));
    }

    private static string ReadStatusValue(string block, string name)
    {
        return TryReadStatusValue(block, name)
            ?? throw new InvalidOperationException($"vcpkg status field '{name}' was absent.");
    }

    private static string? TryReadStatusValue(string block, string name)
    {
        var match = Regex.Match(
            block,
            $"(?m)^{Regex.Escape(name)}: (?<value>[^\\r\\n]+)$",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string ReadCMakeVersion()
    {
        var start = new ProcessStartInfo("cmake", "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("CMake could not be started.");
        var firstLine = process.StandardOutput.ReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0 || firstLine?.StartsWith("cmake version ", StringComparison.Ordinal) is not true)
        {
            throw new InvalidOperationException("CMake version could not be resolved.");
        }

        return firstLine["cmake version ".Length..].Trim();
    }

    private static string ReadMsvcVersion()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        var compiler = Directory.EnumerateFiles(root, "cl.exe", SearchOption.AllDirectories)
            .Where(static path => path.Contains("\\VC\\Tools\\MSVC\\", StringComparison.OrdinalIgnoreCase)
                && path.Contains("\\bin\\Hostx64\\x64\\", StringComparison.OrdinalIgnoreCase))
            .OrderDescending(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The x64 MSVC compiler was not found.");
        var version = FileVersionInfo.GetVersionInfo(compiler).FileVersion
            ?? throw new InvalidOperationException("The MSVC file version was not available.");
        var components = version.Split('.');
        return string.Join('.', components.Take(3));
    }

    private static void ValidateToolchain(CgalProfileManifest profile, CgalResolvedToolchain actual)
    {
        if (actual.Cgal == profile.CgalVersion
            && actual.Gmp == profile.Toolchain.Gmp
            && actual.Mpfr == profile.Toolchain.Mpfr
            && actual.CMake == profile.Toolchain.Cmake
            && actual.Msvc == profile.Toolchain.Msvc)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Installed CGAL toolchain does not match finite profile '{profile.ProfileId}'.");
    }

    [GeneratedRegex("(?m)^\\s*#\\s*include\\s*[<\\\"](?<path>CGAL/[^>\\\"]+)[>\\\"]", RegexOptions.CultureInvariant)]
    private static partial Regex CgalInclude();
}