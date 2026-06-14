using System.Runtime.InteropServices;

using TedToolkit.Occt.Generator.Services.Interfaces;

namespace TedToolkit.Occt.Generator.Services;

internal sealed class VcpkgService : IVcpkgService
{
    public string GetRoot()
    {
#pragma warning disable RS1035
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
#pragma warning restore RS1035

        if (string.IsNullOrEmpty(vcpkgRoot))
        {
            throw new InvalidOperationException("VCPKG_ROOT cannot be empty. Please check your environment variables.");
        }

        return vcpkgRoot;
    }

    public string GetIncludeFolder()
    {
        return Path.Combine(GetRoot(), "installed", GetTriplet(), "include");
    }

    public string GetTriplet()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "x64-windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "x64-linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "arm64-osx"
                : "x64-osx";
        }

        throw new InvalidOperationException("Can't identify which system it is.");
    }

    public string GetOcctIncludeFolder()
    {
        return Path.Combine(GetIncludeFolder(), "opencascade");
    }
}