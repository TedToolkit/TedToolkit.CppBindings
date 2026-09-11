#Requires -Version 7.5

if (-not ('OcctBenchmarkNative.PathIdentity' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OcctBenchmarkNative {
    public sealed class FileIdentityInfo {
        public string Identity { get; set; }
        public uint LinkCount { get; set; }
    }

    public static class PathIdentity {
        private const uint FileReadAttributes = 0x80;
        private const uint FileShareAll = 0x7;
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle handle, StringBuilder path, uint length, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeNameForVolumeMountPoint(
            string volumeMountPoint, StringBuilder volumeName, int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle handle, out ByHandleFileInformation information);

        public static string FinalPath(string path) {
            using (var handle = CreateFile(path, FileReadAttributes, FileShareAll, IntPtr.Zero,
                OpenExisting, BackupSemantics, IntPtr.Zero)) {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateFile failed for " + path);
                var buffer = new StringBuilder(512);
                while (true) {
                    var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                    if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "GetFinalPathNameByHandle failed for " + path);
                    if (length < buffer.Capacity) return RemoveExtendedPrefix(buffer.ToString());
                    buffer.Capacity = checked((int)length + 1);
                }
            }
        }

        public static string VolumeIdentity(string existingPath) {
            var mount = new StringBuilder(1024);
            if (!GetVolumePathName(existingPath, mount, mount.Capacity))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetVolumePathName failed for " + existingPath);
            var volume = new StringBuilder(1024);
            if (!GetVolumeNameForVolumeMountPoint(mount.ToString(), volume, volume.Capacity))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetVolumeNameForVolumeMountPoint failed for " + existingPath);
            return volume.ToString();
        }

        public static string ShortPath(string path) {
            var buffer = new StringBuilder(512);
            while (true) {
                var length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
                if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "GetShortPathName failed for " + path);
                if (length < buffer.Capacity) return buffer.ToString();
                buffer.Capacity = checked((int)length + 1);
            }
        }

        public static FileIdentityInfo FileIdentity(string path) {
            using (var handle = CreateFile(path, FileReadAttributes, FileShareAll, IntPtr.Zero,
                OpenExisting, BackupSemantics, IntPtr.Zero)) {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateFile failed for " + path);
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetFileInformationByHandle failed for " + path);
                if ((information.FileAttributes & 0x10) != 0) throw new InvalidOperationException("Expected a file: " + path);
                return new FileIdentityInfo {
                    Identity = information.VolumeSerialNumber.ToString("X8") + ":" +
                        information.FileIndexHigh.ToString("X8") + information.FileIndexLow.ToString("X8"),
                    LinkCount = information.NumberOfLinks
                };
            }
        }

        private static string RemoveExtendedPrefix(string path) {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) return path.Substring(4);
            return path;
        }
    }
}
'@
}

function Resolve-BenchmarkPhysicalPath {
    param([Parameter(Mandatory)] [string] $Path)

    $full = [IO.Path]::GetFullPath($Path)
    $missing = [Collections.Generic.Stack[string]]::new()
    $existing = $full
    while (-not (Test-Path -LiteralPath $existing)) {
        $name = [IO.Path]::GetFileName($existing.TrimEnd('\', '/'))
        if ([string]::IsNullOrEmpty($name)) { throw "No existing ancestor could be resolved for benchmark path: $full" }
        $missing.Push($name)
        $parent = [IO.Path]::GetDirectoryName($existing.TrimEnd('\', '/'))
        if ([string]::IsNullOrEmpty($parent) -or $parent -ceq $existing) {
            throw "No existing ancestor could be resolved for benchmark path: $full"
        }
        $existing = $parent
    }
    $physical = [OcctBenchmarkNative.PathIdentity]::FinalPath($existing)
    while ($missing.Count -gt 0) { $physical = Join-Path $physical $missing.Pop() }
    return [IO.Path]::GetFullPath($physical)
}

function Get-BenchmarkVolumeIdentity {
    param([Parameter(Mandatory)] [string] $Path)

    $physical = Resolve-BenchmarkPhysicalPath $Path
    $existing = $physical
    while (-not (Test-Path -LiteralPath $existing)) {
        $existing = [IO.Path]::GetDirectoryName($existing.TrimEnd('\', '/'))
        if ([string]::IsNullOrEmpty($existing)) { throw "No existing volume probe ancestor for benchmark path: $physical" }
    }
    return [OcctBenchmarkNative.PathIdentity]::VolumeIdentity($existing)
}

function Get-BenchmarkFileIdentity {
    param([Parameter(Mandatory)] [string] $Path)
    return [OcctBenchmarkNative.PathIdentity]::FileIdentity((Resolve-BenchmarkPhysicalPath $Path))
}

function Test-BenchmarkPathEqual {
    param([string] $Left, [string] $Right)
    return (Resolve-BenchmarkPhysicalPath $Left).TrimEnd('\', '/').Equals(
        (Resolve-BenchmarkPhysicalPath $Right).TrimEnd('\', '/'), [StringComparison]::OrdinalIgnoreCase)
}

function Test-BenchmarkPathWithin {
    param([string] $Path, [string] $Root, [switch] $OrEqual)
    $physicalPath = (Resolve-BenchmarkPhysicalPath $Path).TrimEnd('\', '/')
    $physicalRoot = (Resolve-BenchmarkPhysicalPath $Root).TrimEnd('\', '/')
    return (($OrEqual -and $physicalPath.Equals($physicalRoot, [StringComparison]::OrdinalIgnoreCase)) -or
        $physicalPath.StartsWith($physicalRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase))
}
