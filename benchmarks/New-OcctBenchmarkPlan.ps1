#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $SpecificationDirectory,
    [Parameter(Mandatory)] [string] $BaselineRepositoryRoot,
    [Parameter(Mandatory)] [string] $CandidateRepositoryRoot,
    [Parameter(Mandatory)] [string] $BaselineArtifactRoot,
    [Parameter(Mandatory)] [string] $CandidateArtifactRoot,
    [Parameter(Mandatory)] [string] $BaselineInputVcpkgRoot,
    [Parameter(Mandatory)] [string] $CandidateInputVcpkgRoot,
    [Parameter(Mandatory)] [string] $ToolchainVcpkgRoot,
    [Parameter(Mandatory)] [string] $BaselineOriginalHostReceipt,
    [Parameter(Mandatory)] [string] $BaselineChangedHostReceipt,
    [Parameter(Mandatory)] [string] $CandidateOriginalHostReceipt,
    [Parameter(Mandatory)] [string] $CandidateChangedHostReceipt,
    [Parameter(Mandatory)] [string] $CanonicalArtifactRoot,
    [Parameter(Mandatory)] [string] $CanonicalOriginalManifest,
    [Parameter(Mandatory)] [string] $CanonicalDeclarationManifest,
    [Parameter(Mandatory)] [string] $CanonicalOriginalExportInventory,
    [Parameter(Mandatory)] [string] $CanonicalDeclarationExportInventory,
    [Parameter(Mandatory)] [string] $NativeGateSpecification,
    [Parameter(Mandatory)] [string] $DeclarationHeaderRelativePath,
    [Parameter(Mandatory)] [string] $DeclarationChangedFile,
    [Parameter(Mandatory)] [string] $MissingOutputRelativePath,
    [Parameter(Mandatory)] [string] $DotNetPath,
    [Parameter(Mandatory)] [string] $CMakePath,
    [Parameter(Mandatory)] [string] $NinjaPath,
    [Parameter(Mandatory)] [string] $CompilerPath,
    [Parameter(Mandatory)] [string] $VcVarsPath,
    [Parameter(Mandatory)] [DateTimeOffset] $DeadlineUtc,
    [Parameter(Mandatory)] [long] $MemoryLimitBytes,
    [Parameter(Mandatory)] [long] $MemoryReserveBytes,
    [ValidateSet('screening', 'full')] [string] $Scope = 'full',
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [ValidateRange(1, 64)] [int] $Parallelism = 8,
    [string] $HarnessBinding = 'commit:PENDING-FINAL-HARNESS-COMMIT'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$baselineRevision = 'e94f10a9bb9490d47363cf43d8ce17600b435b8a'
$candidateBehaviorRevision = '9952e5a76358028c22c8ec215a23d7b82413ad4f'
$triplet = 'x64-windows'
$planId = [Guid]::NewGuid().ToString('N')
$utf8 = [Text.UTF8Encoding]::new($false)

function Resolve-ExistingPath {
    param([string] $Path, [string] $Kind)

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if ($Kind -eq 'file' -and -not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Expected a file: $resolved"
    }
    if ($Kind -eq 'directory' -and -not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Expected a directory: $resolved"
    }
    return [IO.Path]::GetFullPath($resolved)
}

function Assert-OrdinaryTree {
    param([string] $Root)

    $item = Get-Item -LiteralPath $Root -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Benchmark input roots must be private physical copies, not reparse points: $Root"
    }
    $reparse = Get-ChildItem -LiteralPath $Root -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $reparse) {
        throw "Benchmark input roots must not contain reparse points: $($reparse.FullName)"
    }
}

function Assert-NoReparseComponents {
    param([string] $Path)
    $current = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrEmpty($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Benchmark paths cannot contain reparse-point components: $($item.FullName)"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -ceq $current) { break }
        $current = $parent
    }
}

function Assert-IsolatedRoots {
    param([hashtable] $Roots)
    $names = @($Roots.Keys)
    for ($leftIndex = 0; $leftIndex -lt $names.Count; $leftIndex++) {
        $left = [IO.Path]::GetFullPath($Roots[$names[$leftIndex]]).TrimEnd('\', '/')
        Assert-NoReparseComponents $left
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $names.Count; $rightIndex++) {
            $right = [IO.Path]::GetFullPath($Roots[$names[$rightIndex]]).TrimEnd('\', '/')
            if ($left.Equals($right, [StringComparison]::OrdinalIgnoreCase) -or
                $left.StartsWith($right + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                $right.StartsWith($left + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Benchmark roots overlap physically or by case: $($names[$leftIndex]) / $($names[$rightIndex])"
            }
        }
    }
}

if (-not ('OcctBenchmarkNative.Volume' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace OcctBenchmarkNative {
    public static class Volume {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeNameForVolumeMountPoint(string volumeMountPoint, StringBuilder volumeName, int bufferLength);
        public static string Identity(string path) {
            var mount = new StringBuilder(1024);
            if (!GetVolumePathName(path, mount, mount.Capacity)) throw new InvalidOperationException("GetVolumePathName failed: " + Marshal.GetLastWin32Error());
            var volume = new StringBuilder(1024);
            if (!GetVolumeNameForVolumeMountPoint(mount.ToString(), volume, volume.Capacity)) throw new InvalidOperationException("GetVolumeNameForVolumeMountPoint failed: " + Marshal.GetLastWin32Error());
            return volume.ToString();
        }
    }
}
'@
}

function Get-VolumeIdentity {
    param([string] $Path)
    [OcctBenchmarkNative.Volume]::Identity([IO.Path]::GetFullPath($Path))
}

function Get-GitOutput {
    param([string] $Repository, [string[]] $Arguments)

    $output = @(& git -c "safe.directory=$Repository" -C $Repository @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "git failed for $Repository with arguments: $($Arguments -join ' ')" }
    return ($output -join "`n").Trim()
}

function Write-NewJson {
    param([string] $Path, $Value, [int] $Depth = 20)

    $parent = [IO.Path]::GetDirectoryName($Path)
    $null = [IO.Directory]::CreateDirectory($parent)
    $bytes = $utf8.GetBytes(($Value | ConvertTo-Json -Depth $Depth))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes) }
    finally { $stream.Dispose() }
}

function Get-FileBinding {
    param([string] $Path)

    [ordered]@{
        Path = $Path
        Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
}

function Read-HostReceipt {
    param([string] $Path, [string] $ExpectedVariant, [string] $ExpectedState, [string] $ExpectedBase)

    $resolved = Resolve-ExistingPath $Path file
    $receipt = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json -AsHashtable -DateKind String
    if ($receipt.SchemaVersion -ne 1 -or $receipt.ReceiptKind -cne 'occt-console-host' -or
        $receipt.Variant -cne $ExpectedVariant -or $receipt.State -cne $ExpectedState -or
        -not $receipt.SourceClean -or $receipt.HostFiles -isnot [array] -or $receipt.HostFiles.Count -lt 3) {
        throw "Invalid Console host receipt: $resolved"
    }
    $hostRoot = Resolve-ExistingPath $receipt.HostDirectory directory
    Assert-NoReparseComponents $hostRoot
    Assert-OrdinaryTree $hostRoot
    $entryPoint = [IO.Path]::GetFullPath((Join-Path $hostRoot $receipt.HostEntryPointRelativePath))
    $prefix = $hostRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $entryPoint.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A host receipt entry point escapes its complete-host directory.'
    }
    try { $assembly = [Reflection.AssemblyName]::GetAssemblyName($entryPoint) }
    catch { throw "A host receipt does not reference a valid managed assembly: $entryPoint" }
    if ($assembly.Name -cne 'TedToolkit.CppBindings.Occt.Console' -or
        $assembly.FullName -cne $receipt.HostAssemblyIdentity) {
        throw 'A host receipt has the wrong managed assembly identity.'
    }
    $actual = @(Get-ChildItem -LiteralPath $hostRoot -Recurse -File | Sort-Object FullName)
    if ($actual.Count -ne $receipt.HostFiles.Count) { throw 'The complete-host directory inventory changed.' }
    for ($index = 0; $index -lt $actual.Count; $index++) {
        $recorded = $receipt.HostFiles[$index]
        $relative = [IO.Path]::GetRelativePath($hostRoot, $actual[$index].FullName).Replace('\', '/')
        if ($relative -cne $recorded.Path -or $actual[$index].Length -ne $recorded.Bytes -or
            (Get-FileHash -LiteralPath $actual[$index].FullName -Algorithm SHA256).Hash -cne $recorded.Sha256) {
            throw "The complete-host receipt changed: $relative"
        }
    }
    foreach ($bindingName in @('BuildSpecification', 'BuildResult')) {
        $boundPath = Resolve-ExistingPath $receipt["${bindingName}Path"] file
        if ((Get-FileHash -LiteralPath $boundPath -Algorithm SHA256).Hash -cne $receipt["${bindingName}Sha256"]) {
            throw "The host $bindingName receipt changed."
        }
    }
    if (-not $receipt.FixtureOnly) {
        if ($receipt.SourceBaseRevision -cne $ExpectedBase) {
            throw "$ExpectedVariant/$ExpectedState host provenance has the wrong exact source base."
        }
        $sourceRoot = Resolve-ExistingPath $receipt.SourceRepositoryRoot directory
        Assert-NoReparseComponents $sourceRoot
        if ((Get-GitOutput $sourceRoot @('rev-parse', 'HEAD')) -cne $receipt.SourceRevision -or
            (Get-GitOutput $sourceRoot @('status', '--porcelain=v1', '--untracked-files=all', '--ignore-submodules=all'))) {
            throw "$ExpectedVariant/$ExpectedState host provenance source is not at its clean recorded revision."
        }
        if ($ExpectedState -eq 'original') {
            if ($receipt.SourceRevision -cne $ExpectedBase -or $receipt.FrozenPatchPath) {
                throw 'Original host provenance must have no source delta.'
            }
        }
        else {
            $patchPath = Resolve-ExistingPath $receipt.FrozenPatchPath file
            $patchHash = (Get-FileHash -LiteralPath $patchPath -Algorithm SHA256).Hash
            if ($patchHash -cne $receipt.FrozenPatchSha256 -or $receipt.SourceRevision -ceq $ExpectedBase) {
                throw 'Changed host provenance has an invalid frozen patch binding.'
            }
            $lines = @(& git -c "safe.directory=$sourceRoot" -C $sourceRoot diff --binary --full-index --no-ext-diff `
                $ExpectedBase $receipt.SourceRevision -- 2>$null)
            if ($LASTEXITCODE -ne 0) { throw 'Changed host provenance source delta could not be reproduced.' }
            $deltaBytes = if ($lines.Count -eq 0) { [byte[]]::new(0) } else { $utf8.GetBytes(($lines -join "`n") + "`n") }
            if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($deltaBytes)) -cne $patchHash) {
                throw 'Changed host source delta is not exactly its frozen patch.'
            }
        }
    }
    return [pscustomobject]@{ Path = $resolved; Value = $receipt; HostRoot = $hostRoot; EntryPoint = $entryPoint }
}

function Read-CanonicalManifest {
    param([string] $Path)
    $resolved = Resolve-ExistingPath $Path file
    $value = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json -AsHashtable -DateKind String
    if ($value.SchemaVersion -ne 1 -or $value.FileCount -le 0 -or $value.Files -isnot [array] -or
        $value.Files.Count -ne $value.FileCount) { throw "Invalid canonical artifact manifest: $resolved" }
    return [pscustomobject]@{ Path = $resolved; Value = $value }
}

function Read-ExportInventory {
    param([string] $Path)
    $resolved = Resolve-ExistingPath $Path file
    $value = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json -AsHashtable -DateKind String
    if ($value.SchemaVersion -ne 1 -or $value.ExportCount -le 0 -or $value.Exports -isnot [array] -or
        $value.Exports.Count -ne $value.ExportCount -or
        @($value.Exports | Sort-Object -Unique).Count -ne $value.Exports.Count) {
        throw "Invalid canonical export inventory: $resolved"
    }
    return [pscustomobject]@{ Path = $resolved; Value = $value }
}

function Assert-ManifestContentEqual {
    param($Actual, $Expected, [string] $Label)
    if ($Actual.FileCount -ne $Expected.FileCount -or $Actual.TotalBytes -ne $Expected.TotalBytes) {
        throw "$Label does not match the canonical artifact inventory."
    }
    for ($index = 0; $index -lt $Actual.Files.Count; $index++) {
        $left = $Actual.Files[$index]
        $right = $Expected.Files[$index]
        if ($left.Path -cne $right.Path -or $left.Bytes -ne $right.Bytes -or $left.Sha256 -cne $right.Sha256) {
            throw "$Label differs from the canonical artifact inventory at $($left.Path)."
        }
    }
}

function Assert-SafeArtifactRoot {
    param([string] $Root, [string[]] $RepositoryRoots, [string[]] $ForbiddenRoots)

    $full = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ($full -ceq [IO.Path]::GetPathRoot($full).TrimEnd('\', '/') -or $full.Length -lt 12) {
        throw "Artifact root is too broad for managed cleanup: $full"
    }
    foreach ($repository in $RepositoryRoots) {
        $other = [IO.Path]::GetFullPath($repository).TrimEnd('\', '/')
        if ($full -ceq $other -or $other.StartsWith($full + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Artifact root is too broad and overlaps a repository: $full"
        }
    }
    foreach ($protected in $ForbiddenRoots) {
        $other = [IO.Path]::GetFullPath($protected).TrimEnd('\', '/')
        if ($full -ceq $other -or $full.StartsWith($other + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase) -or
            $other.StartsWith($full + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Artifact root overlaps a protected repository, input, or toolchain root: $full"
        }
    }
}

function New-Stage {
    param(
        [string] $Directory,
        [string] $Variant,
        [string] $Workload,
        [string] $Action,
        [int] $TimeLimitSeconds
    )

    $path = Join-Path $Directory "$Variant-$Workload-$($Action.ToLowerInvariant()).json"
    $arguments = @(
        '-NoProfile', '-File', $adapterPath,
        '-PlanPath', $planPath,
        '-Action', $Action,
        '-Variant', $Variant,
        '-Workload', $Workload,
        '-SampleRoot', '{SampleRoot}'
    )
    Write-NewJson $path ([ordered]@{
        Executable = $powerShellPath
        Arguments = $arguments
        WorkingDirectory = $destination
        TimeLimitSeconds = $TimeLimitSeconds
    })
    return $path
}

function Get-ToolchainSnapshot {
    $environmentLines = @(& $env:COMSPEC /d /c "call `"$vcvars`" >nul && set")
    if ($LASTEXITCODE -ne 0) { throw 'The pinned vcvars64 environment could not be initialized.' }
    $compilerEnvironment = @{}
    foreach ($line in $environmentLines) {
        if ($line -match '^([^=]+)=(.*)$') { $compilerEnvironment[$Matches[1]] = $Matches[2] }
    }
    $requiredEnvironment = @(
        'VCToolsInstallDir', 'WindowsSdkDir', 'WindowsSDKVersion',
        'VSCMD_ARG_HOST_ARCH', 'VSCMD_ARG_TGT_ARCH', 'INCLUDE', 'LIB', 'LIBPATH'
    )
    foreach ($name in $requiredEnvironment) {
        if (-not $compilerEnvironment.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($compilerEnvironment[$name])) {
            throw "vcvars64 did not select required environment member: $name"
        }
    }
    if ($compilerEnvironment.VSCMD_ARG_HOST_ARCH -cne 'x64' -or
        $compilerEnvironment.VSCMD_ARG_TGT_ARCH -cne 'x64') {
        throw 'vcvars64 did not select the x64 host and target.'
    }
    $selectedCompiler = [IO.Path]::GetFullPath((Join-Path $compilerEnvironment.VCToolsInstallDir 'bin/Hostx64/x64/cl.exe'))
    if (-not (Test-Path -LiteralPath $selectedCompiler -PathType Leaf) -or
        -not $selectedCompiler.Equals($compiler, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The provided compiler is not the cl.exe selected by VCToolsInstallDir.'
    }
    $previous = @{}
    try {
        foreach ($entry in $compilerEnvironment.GetEnumerator()) {
            $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
        # NUL gives cl.exe a no-op input so /Bv reports identity with a successful exit code.
        $compilerBv = @(& $compiler /Bv /c NUL 2>&1)
        if ($LASTEXITCODE -ne 0 -or $compilerBv.Count -eq 0) { throw 'The selected compiler /Bv probe failed.' }
        $cmakeVersion = @(& $cmake --version 2>&1)
        if ($LASTEXITCODE -ne 0 -or $cmakeVersion.Count -eq 0) { throw 'The pinned CMake version probe failed.' }
        $ninjaVersion = @(& $ninja --version 2>&1)
        if ($LASTEXITCODE -ne 0 -or $ninjaVersion.Count -ne 1) { throw 'The pinned Ninja version probe failed.' }
        $dotnetInfo = @(& $dotnet --info 2>&1)
        if ($LASTEXITCODE -ne 0 -or $dotnetInfo.Count -eq 0) { throw 'The pinned dotnet --info probe failed.' }
    }
    finally {
        foreach ($entry in $previous.GetEnumerator()) {
            $value = if ($null -eq $entry.Value) { [NullString]::Value } else { $entry.Value }
            [Environment]::SetEnvironmentVariable($entry.Key, $value, 'Process')
        }
    }
    return [ordered]@{
        VCToolsInstallDir = $compilerEnvironment.VCToolsInstallDir
        WindowsSdkDir = $compilerEnvironment.WindowsSdkDir
        WindowsSDKVersion = $compilerEnvironment.WindowsSDKVersion
        HostArchitecture = $compilerEnvironment.VSCMD_ARG_HOST_ARCH
        TargetArchitecture = $compilerEnvironment.VSCMD_ARG_TGT_ARCH
        Include = $compilerEnvironment.INCLUDE
        Lib = $compilerEnvironment.LIB
        LibPath = $compilerEnvironment.LIBPATH
        CompilerBv = $compilerBv -join "`n"
        CMakeVersion = $cmakeVersion -join "`n"
        NinjaVersion = $ninjaVersion -join "`n"
        DotNetInfo = $dotnetInfo -join "`n"
    }
}

$destination = [IO.Path]::GetFullPath($SpecificationDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Use a new OCCT benchmark specification directory.' }
if ($DeadlineUtc.ToUniversalTime() -le [DateTimeOffset]::UtcNow -or
    $DeadlineUtc.ToUniversalTime() -gt [DateTimeOffset]::UtcNow.AddHours(12)) {
    throw 'The shared deadline must be unexpired and no more than 12 hours away.'
}
if ($MemoryLimitBytes -le 0 -or $MemoryReserveBytes -le 0) {
    throw 'Positive memory limit and reserve values are required.'
}
if ($HarnessBinding -cne 'commit:PENDING-FINAL-HARNESS-COMMIT' -and
    $HarnessBinding -notmatch '^commit:[0-9a-f]{40}$') {
    throw 'HarnessBinding must be the explicit pending marker or a lowercase full commit binding.'
}
if ([IO.Path]::IsPathRooted($DeclarationHeaderRelativePath) -or
    $DeclarationHeaderRelativePath.Contains('..', [StringComparison]::Ordinal) -or
    -not $DeclarationHeaderRelativePath.EndsWith('.hxx', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DeclarationHeaderRelativePath must be a safe relative .hxx path.'
}
if ([IO.Path]::IsPathRooted($MissingOutputRelativePath) -or
    $MissingOutputRelativePath.Contains('..', [StringComparison]::Ordinal) -or
    -not $MissingOutputRelativePath.Replace('\', '/').StartsWith('cpp/', [StringComparison]::Ordinal) -or
    -not $MissingOutputRelativePath.EndsWith('.cpp', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'MissingOutputRelativePath must select one generated cpp/*.cpp file.'
}

$baselineRepository = Resolve-ExistingPath $BaselineRepositoryRoot directory
$candidateRepository = Resolve-ExistingPath $CandidateRepositoryRoot directory
Assert-NoReparseComponents $baselineRepository
Assert-NoReparseComponents $candidateRepository
if ((Get-GitOutput $baselineRepository @('rev-parse', 'HEAD')) -cne $baselineRevision) {
    throw "The baseline repository must be exactly $baselineRevision."
}
if (Get-GitOutput $baselineRepository @('status', '--porcelain=v1', '--untracked-files=all', '--ignore-submodules=all')) {
    throw 'The baseline repository must be clean.'
}
$candidateHead = Get-GitOutput $candidateRepository @('rev-parse', 'HEAD')
$null = Get-GitOutput $candidateRepository @('merge-base', '--is-ancestor', $candidateBehaviorRevision, $candidateHead)
if (Get-GitOutput $candidateRepository @('status', '--porcelain=v1', '--untracked-files=all', '--ignore-submodules=all')) {
    throw 'The candidate repository must be clean before a plan is frozen.'
}
if ($HarnessBinding -match '^commit:(?<sha>[0-9a-f]{40})$' -and $Matches.sha -cne $candidateHead) {
    throw 'The final HarnessBinding must equal the candidate repository HEAD.'
}

$baselineArtifact = [IO.Path]::GetFullPath($BaselineArtifactRoot)
$candidateArtifact = [IO.Path]::GetFullPath($CandidateArtifactRoot)
if ($baselineArtifact.Length -ne $candidateArtifact.Length) {
    throw 'Baseline and candidate artifact roots must have equal absolute path lengths.'
}
if ($baselineArtifact -ceq $candidateArtifact) { throw 'Artifact roots must be isolated.' }
$baselineInput = Resolve-ExistingPath $BaselineInputVcpkgRoot directory
$candidateInput = Resolve-ExistingPath $CandidateInputVcpkgRoot directory
$toolchainVcpkg = Resolve-ExistingPath $ToolchainVcpkgRoot directory
if ($baselineInput.Length -ne $candidateInput.Length) {
    throw 'Baseline and candidate input-vcpkg roots must have equal absolute path lengths.'
}
if ($baselineInput -ceq $candidateInput -or $baselineInput -ceq $toolchainVcpkg -or
    $candidateInput -ceq $toolchainVcpkg) {
    throw 'Private editable input roots and the real vcpkg toolchain root must remain separate.'
}
Assert-SafeArtifactRoot $baselineArtifact @($baselineRepository, $candidateRepository) @($baselineInput,
    $candidateInput, $toolchainVcpkg)
Assert-SafeArtifactRoot $candidateArtifact @($baselineRepository, $candidateRepository) @($baselineInput,
    $candidateInput, $toolchainVcpkg)

$includeRoots = [ordered]@{
    baseline = Join-Path $baselineInput "installed/$triplet/include"
    candidate = Join-Path $candidateInput "installed/$triplet/include"
}
foreach ($includeRoot in $includeRoots.Values) {
    $null = Resolve-ExistingPath $includeRoot directory
    Assert-OrdinaryTree $includeRoot
}
$baselineHeader = Resolve-ExistingPath (Join-Path $includeRoots.baseline $DeclarationHeaderRelativePath) file
$candidateHeader = Resolve-ExistingPath (Join-Path $includeRoots.candidate $DeclarationHeaderRelativePath) file
$changedHeader = Resolve-ExistingPath $DeclarationChangedFile file
if ((Get-FileHash $baselineHeader).Hash -cne (Get-FileHash $candidateHeader).Hash) {
    throw 'The private baseline and candidate declaration headers must start byte-identical.'
}
if ((Get-FileHash $baselineHeader).Hash -ceq (Get-FileHash $changedHeader).Hash) {
    throw 'The declaration-edit input must differ from the original header.'
}

$toolchainFile = Resolve-ExistingPath (Join-Path $toolchainVcpkg 'scripts/buildsystems/vcpkg.cmake') file
$toolchainStatus = Resolve-ExistingPath (Join-Path $toolchainVcpkg 'installed/vcpkg/status') file
$statusFiles = [ordered]@{
    baseline = Resolve-ExistingPath (Join-Path $baselineInput 'installed/vcpkg/status') file
    candidate = Resolve-ExistingPath (Join-Path $candidateInput 'installed/vcpkg/status') file
}
if ((Get-FileHash $statusFiles.baseline).Hash -cne (Get-FileHash $statusFiles.candidate).Hash) {
    throw 'The private input roots must use the same vcpkg status file.'
}
if ((Get-FileHash $statusFiles.baseline).Hash -cne (Get-FileHash $toolchainStatus).Hash) {
    throw 'Private header inputs and the real vcpkg toolchain must describe the same installed packages.'
}

$hostReceipts = [ordered]@{
    baseline = [ordered]@{
        original = Read-HostReceipt $BaselineOriginalHostReceipt baseline original $baselineRevision
        changed = Read-HostReceipt $BaselineChangedHostReceipt baseline changed $baselineRevision
    }
    candidate = [ordered]@{
        original = Read-HostReceipt $CandidateOriginalHostReceipt candidate original $candidateHead
        changed = Read-HostReceipt $CandidateChangedHostReceipt candidate changed $candidateHead
    }
}
$hosts = [ordered]@{
    baseline = [ordered]@{ original = $hostReceipts.baseline.original.EntryPoint; changed = $hostReceipts.baseline.changed.EntryPoint }
    candidate = [ordered]@{ original = $hostReceipts.candidate.original.EntryPoint; changed = $hostReceipts.candidate.changed.EntryPoint }
}
$hostDirectories = @($hostReceipts.baseline.original.HostRoot, $hostReceipts.baseline.changed.HostRoot,
    $hostReceipts.candidate.original.HostRoot, $hostReceipts.candidate.changed.HostRoot)
if (@($hostDirectories | Sort-Object -Unique).Count -ne 4) { throw 'Every measured host requires its own complete directory receipt.' }
if ($hosts.baseline.original.Length -ne $hosts.candidate.original.Length -or
    $hosts.baseline.changed.Length -ne $hosts.candidate.changed.Length) {
    throw 'Corresponding measured Console host paths must have equal absolute lengths.'
}
if ($hostReceipts.baseline.changed.Value.FrozenPatchSha256 -cne
    $hostReceipts.candidate.changed.Value.FrozenPatchSha256) {
    throw 'Baseline and candidate changed hosts must use the same frozen source delta.'
}
foreach ($variant in @('baseline', 'candidate')) {
    $expectedRepository = if ($variant -eq 'baseline') { $baselineRepository } else { $candidateRepository }
    $receipt = $hostReceipts[$variant].original.Value
    if (-not $receipt.FixtureOnly -and
        -not [IO.Path]::GetFullPath($receipt.SourceRepositoryRoot).Equals($expectedRepository,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$variant original host must be built from the measured clean repository root."
    }
}

$canonicalRoot = Resolve-ExistingPath $CanonicalArtifactRoot directory
Assert-OrdinaryTree $canonicalRoot
$canonicalManifests = [ordered]@{
    original = Read-CanonicalManifest $CanonicalOriginalManifest
    declaration = Read-CanonicalManifest $CanonicalDeclarationManifest
}
$canonicalExports = [ordered]@{
    original = Read-ExportInventory $CanonicalOriginalExportInventory
    declaration = Read-ExportInventory $CanonicalDeclarationExportInventory
}
$gateSpecPath = Resolve-ExistingPath $NativeGateSpecification file
$gateSpec = Get-Content -LiteralPath $gateSpecPath -Raw | ConvertFrom-Json -AsHashtable -DateKind String
if ($gateSpec.SchemaVersion -ne 1 -or $gateSpec.FixtureOnly -isnot [bool] -or $gateSpec.Arguments -isnot [array] -or
    $gateSpec.Arguments.Count -eq 0 -or $gateSpec.InputFiles -isnot [array] -or
    $gateSpec.InputFiles.Count -eq 0) { throw 'Invalid native boundary/integration gate specification.' }
$gateExecutable = Resolve-ExistingPath $gateSpec.Executable file
$gateWorkingDirectory = Resolve-ExistingPath $gateSpec.WorkingDirectory directory
$allowedGatePlaceholders = @('{ArtifactRoot}', '{GeneratedRoot}', '{NativeBuildRoot}', '{Variant}', '{Workload}', '{Phase}')
foreach ($argument in $gateSpec.Arguments) {
    if ($argument -isnot [string]) { throw 'Native gate arguments must be strings.' }
    if ($argument -match '^\{[^{}]+\}$' -and $argument -cnotin $allowedGatePlaceholders) {
        throw "Unknown native gate placeholder: $argument"
    }
    foreach ($placeholder in $allowedGatePlaceholders) {
        if ($argument -cne $placeholder -and $argument.Contains($placeholder, [StringComparison]::Ordinal)) {
            throw 'Native gate placeholders must occupy a whole argument.'
        }
    }
}
if (@($gateSpec.Arguments | Where-Object { $_ -in @('{ArtifactRoot}', '{GeneratedRoot}', '{NativeBuildRoot}') }).Count -eq 0) {
    throw 'The native gate must receive an exact artifact boundary path.'
}
foreach ($binding in $gateSpec.InputFiles) {
    $path = Resolve-ExistingPath $binding.Path file
    if ($binding.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $binding.Sha256) {
        throw "Native gate input changed: $path"
    }
}

$dotnet = Resolve-ExistingPath $DotNetPath file
$cmake = Resolve-ExistingPath $CMakePath file
$ninja = Resolve-ExistingPath $NinjaPath file
$compiler = Resolve-ExistingPath $CompilerPath file
$vcvars = Resolve-ExistingPath $VcVarsPath file
$powerShellPath = (Get-Process -Id $PID).Path
$adapterPath = Resolve-ExistingPath (Join-Path $PSScriptRoot 'Invoke-OcctBenchmarkWorkload.ps1') file
$manifestTool = Resolve-ExistingPath (Join-Path $PSScriptRoot 'Get-ArtifactManifest.ps1') file
$ninjaMetricsTool = Resolve-ExistingPath (Join-Path $PSScriptRoot 'Get-NinjaBuildMetrics.ps1') file
$exportInventoryTool = Resolve-ExistingPath (Join-Path $PSScriptRoot 'Get-OcctExportInventory.ps1') file
$toolchainSnapshot = Get-ToolchainSnapshot

$isolationRoots = [ordered]@{
    specification = $destination
    baselineRepository = $baselineRepository
    candidateRepository = $candidateRepository
    baselineInput = $baselineInput
    candidateInput = $candidateInput
    toolchainVcpkg = $toolchainVcpkg
    baselineArtifact = $baselineArtifact
    candidateArtifact = $candidateArtifact
    canonicalArtifact = $canonicalRoot
    baselineOriginalHost = $hostReceipts.baseline.original.HostRoot
    baselineChangedHost = $hostReceipts.baseline.changed.HostRoot
    candidateOriginalHost = $hostReceipts.candidate.original.HostRoot
    candidateChangedHost = $hostReceipts.candidate.changed.HostRoot
}
Assert-IsolatedRoots $isolationRoots
foreach ($variant in @('baseline', 'candidate')) {
    $changedReceipt = $hostReceipts[$variant].changed.Value
    if (-not $changedReceipt.FixtureOnly) {
        $changedSource = Resolve-ExistingPath $changedReceipt.SourceRepositoryRoot directory
        foreach ($root in $isolationRoots.Values) {
            $left = [IO.Path]::GetFullPath($changedSource).TrimEnd('\', '/')
            $right = [IO.Path]::GetFullPath($root).TrimEnd('\', '/')
            if ($left.Equals($right, [StringComparison]::OrdinalIgnoreCase) -or
                $left.StartsWith($right + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                $right.StartsWith($left + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "$variant changed-host source repository overlaps another benchmark root."
            }
        }
        $isolationRoots["${variant}ChangedSource"] = $changedSource
    }
}

$null = [IO.Directory]::CreateDirectory($destination)
$inputDirectory = Join-Path $destination 'inputs'
$null = [IO.Directory]::CreateDirectory($inputDirectory)
$originalHeaderCopy = Join-Path $inputDirectory 'declaration-original.hxx'
$changedHeaderCopy = Join-Path $inputDirectory 'declaration-changed.hxx'
[IO.File]::Copy($baselineHeader, $originalHeaderCopy)
[IO.File]::Copy($changedHeader, $changedHeaderCopy)

$inputManifestPath = Join-Path $inputDirectory 'private-input-manifest.json'
$inputEntries = [Collections.Generic.List[object]]::new()
foreach ($variant in @('baseline', 'candidate')) {
    $root = $includeRoots[$variant]
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        if ($relative -ceq $DeclarationHeaderRelativePath.Replace('\', '/')) { continue }
        $inputEntries.Add([ordered]@{
            Variant = $variant
            Path = $relative
            Bytes = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        })
    }
}
$baselineInputEntries = @($inputEntries | Where-Object Variant -CEQ baseline)
$candidateInputEntries = @($inputEntries | Where-Object Variant -CEQ candidate)
if ($baselineInputEntries.Count -ne $candidateInputEntries.Count) {
    throw 'The private baseline and candidate include inventories differ.'
}
for ($index = 0; $index -lt $baselineInputEntries.Count; $index++) {
    $left = $baselineInputEntries[$index]
    $right = $candidateInputEntries[$index]
    if ($left.Path -cne $right.Path -or $left.Bytes -ne $right.Bytes -or $left.Sha256 -cne $right.Sha256) {
        throw "The private baseline and candidate include inventories differ: $($left.Path) / $($right.Path)"
    }
}
Write-NewJson $inputManifestPath ([ordered]@{
    SchemaVersion = 1
    HeaderRelativePath = $DeclarationHeaderRelativePath.Replace('\', '/')
    OriginalHeaderSha256 = (Get-FileHash $originalHeaderCopy).Hash
    ChangedHeaderSha256 = (Get-FileHash $changedHeaderCopy).Hash
    Entries = @($inputEntries.ToArray())
}) 6

$canonicalGenerated = Join-Path $canonicalRoot 'generated'
$canonicalCSharp = Resolve-ExistingPath (Join-Path $canonicalGenerated 'csharp') directory
$canonicalCpp = Resolve-ExistingPath (Join-Path $canonicalGenerated 'cpp') directory
$canonicalUnsupported = Resolve-ExistingPath (Join-Path $canonicalGenerated 'unsupported-headers.txt') file
$canonicalLibrary = Resolve-ExistingPath (Join-Path $canonicalRoot "native-build/$Configuration/ted_toolkit_occt.dll") file
if ((Get-Item -LiteralPath $canonicalLibrary).Length -eq 0) { throw 'The canonical native boundary library is empty.' }
$canonicalCheckManifest = Join-Path $inputDirectory 'canonical-original-validated.json'
& $powerShellPath -NoProfile -File $manifestTool -Roots $canonicalCSharp $canonicalCpp -Files $canonicalUnsupported `
    -ReportPath $canonicalCheckManifest
if ($LASTEXITCODE -ne 0) { throw 'The canonical baseline artifact root could not be inventoried.' }
$canonicalActual = Get-Content -LiteralPath $canonicalCheckManifest -Raw | ConvertFrom-Json -AsHashtable -DateKind String
Assert-ManifestContentEqual $canonicalActual $canonicalManifests.original.Value 'Canonical baseline root'
$canonicalCheckExports = Join-Path $inputDirectory 'canonical-exports-validated.json'
& $powerShellPath -NoProfile -File $exportInventoryTool -SourcePath (Join-Path $canonicalCpp 'NativeFunctionTable.cpp') `
    -ReportPath $canonicalCheckExports -CompareTo $canonicalExports.original.Path
if ($LASTEXITCODE -ne 0 -or
    -not (Get-Content -LiteralPath $canonicalCheckExports -Raw | ConvertFrom-Json).Comparison.EqualOrderedExports) {
    throw 'The canonical baseline root does not match its ordered export oracle.'
}

$artifacts = [ordered]@{ baseline = $baselineArtifact; candidate = $candidateArtifact }
foreach ($variant in @('baseline', 'candidate')) {
    $root = $artifacts[$variant]
    if (Test-Path -LiteralPath $root) {
        if (-not (Test-Path -LiteralPath $root -PathType Container) -or
            @(Get-ChildItem -LiteralPath $root -Force).Count -ne 0) {
            throw "Artifact root must be absent or empty while freezing a plan: $root"
        }
    }
    else { $null = [IO.Directory]::CreateDirectory($root) }
    Write-NewJson (Join-Path $root '.occt-benchmark-root.json') ([ordered]@{
        SchemaVersion = 1; PlanId = $planId; Variant = $variant
    })
}
$artifactVolume = Get-VolumeIdentity $baselineArtifact
if ($artifactVolume -cne (Get-VolumeIdentity $candidateArtifact) -or
    $artifactVolume -cne (Get-VolumeIdentity $candidateRepository)) {
    throw 'Measured artifact roots and the repository resource-preflight root must share one physical volume.'
}

$planPath = Join-Path $destination 'occt-plan.json'
$receiptValues = @($hostReceipts.baseline.original.Value, $hostReceipts.baseline.changed.Value,
    $hostReceipts.candidate.original.Value, $hostReceipts.candidate.changed.Value)
$fixtureOnlyPlan = [bool] ($gateSpec.FixtureOnly -or @($receiptValues | Where-Object FixtureOnly).Count -gt 0)
$fileBindings = [Collections.Generic.List[object]]::new()
foreach ($path in @($inputManifestPath, $originalHeaderCopy, $changedHeaderCopy, $adapterPath,
        $manifestTool, $ninjaMetricsTool, $exportInventoryTool, $dotnet, $cmake, $ninja, $compiler, $vcvars, $toolchainFile,
        $toolchainStatus, $statusFiles.baseline, $statusFiles.candidate, $hosts.baseline.original, $hosts.baseline.changed,
        $hosts.candidate.original, $hosts.candidate.changed, $hostReceipts.baseline.original.Path,
        $hostReceipts.baseline.changed.Path, $hostReceipts.candidate.original.Path, $hostReceipts.candidate.changed.Path,
        $canonicalManifests.original.Path, $canonicalManifests.declaration.Path, $canonicalExports.original.Path,
        $canonicalExports.declaration.Path, $canonicalCheckManifest, $canonicalCheckExports, $gateSpecPath,
        $gateExecutable, $canonicalLibrary)) {
    $fileBindings.Add((Get-FileBinding $path))
}
foreach ($receipt in $receiptValues) {
    foreach ($path in @($receipt.BuildSpecificationPath, $receipt.BuildResultPath, $receipt.FrozenPatchPath) |
        Where-Object { $_ }) { $fileBindings.Add((Get-FileBinding $path)) }
}
foreach ($binding in $gateSpec.InputFiles) { $fileBindings.Add((Get-FileBinding (Resolve-ExistingPath $binding.Path file))) }

$plan = [ordered]@{
    SchemaVersion = 1
    PlanId = $planId
    ProductionAdoptionAuthorized = $false
    BaselineRevision = $baselineRevision
    CandidateBehaviorRevision = $candidateBehaviorRevision
    CandidateHead = $candidateHead
    HarnessBinding = $HarnessBinding
    FixtureOnly = $fixtureOnlyPlan
    Scope = $Scope
    Configuration = $Configuration
    Parallelism = $Parallelism
    Triplet = $triplet
    DeclarationHeaderRelativePath = $DeclarationHeaderRelativePath.Replace('\', '/')
    MissingOutputRelativePath = $MissingOutputRelativePath.Replace('\', '/')
    OriginalHeaderFile = $originalHeaderCopy
    ChangedHeaderFile = $changedHeaderCopy
    GeneratorChangePatchSha256 = $hostReceipts.baseline.changed.Value.FrozenPatchSha256
    FrozenInputManifest = $inputManifestPath
    FrozenFileBindings = @($fileBindings.ToArray())
    ToolchainVcpkgRoot = $toolchainVcpkg
    ToolchainFile = $toolchainFile
    ToolchainStatusFile = $toolchainStatus
    ToolchainSnapshot = $toolchainSnapshot
    ArtifactVolumeIdentity = $artifactVolume
    MeasuredPathShape = [ordered]@{
        ArtifactRootLength = $baselineArtifact.Length
        InputVcpkgRootLength = $baselineInput.Length
        OriginalHostLength = $hosts.baseline.original.Length
        ChangedHostLength = $hosts.baseline.changed.Length
        NeutralWorkingDirectory = $destination
    }
    Canonical = [ordered]@{
        ArtifactRoot = $canonicalRoot
        NativeLibrary = $canonicalLibrary
        OriginalManifest = $canonicalManifests.original.Path
        DeclarationManifest = $canonicalManifests.declaration.Path
        OriginalExportInventory = $canonicalExports.original.Path
        DeclarationExportInventory = $canonicalExports.declaration.Path
    }
    NativeGate = [ordered]@{
        SpecificationPath = $gateSpecPath
        Executable = $gateExecutable
        Arguments = $gateSpec.Arguments
        WorkingDirectory = $gateWorkingDirectory
        FixtureOnly = $gateSpec.FixtureOnly
    }
    Tools = [ordered]@{
        DotNet = $dotnet; CMake = $cmake; Ninja = $ninja; Compiler = $compiler; VcVars = $vcvars
        Manifest = $manifestTool; NinjaMetrics = $ninjaMetricsTool; ExportInventory = $exportInventoryTool
    }
    Variants = [ordered]@{
        baseline = [ordered]@{
            RepositoryRoot = $baselineRepository; ArtifactRoot = $baselineArtifact; InputVcpkgRoot = $baselineInput
            IncludeRoot = $includeRoots.baseline; HeaderPath = $baselineHeader; StatusFile = $statusFiles.baseline
            StatusFileSha256 = (Get-FileHash $statusFiles.baseline).Hash
            OriginalHost = $hosts.baseline.original; OriginalHostRoot = $hostReceipts.baseline.original.HostRoot
            OriginalHostSha256 = (Get-FileHash $hosts.baseline.original).Hash
            ChangedHost = $hosts.baseline.changed; ChangedHostRoot = $hostReceipts.baseline.changed.HostRoot
            ChangedHostSha256 = (Get-FileHash $hosts.baseline.changed).Hash
        }
        candidate = [ordered]@{
            RepositoryRoot = $candidateRepository; ArtifactRoot = $candidateArtifact; InputVcpkgRoot = $candidateInput
            IncludeRoot = $includeRoots.candidate; HeaderPath = $candidateHeader; StatusFile = $statusFiles.candidate
            StatusFileSha256 = (Get-FileHash $statusFiles.candidate).Hash
            OriginalHost = $hosts.candidate.original; OriginalHostRoot = $hostReceipts.candidate.original.HostRoot
            OriginalHostSha256 = (Get-FileHash $hosts.candidate.original).Hash
            ChangedHost = $hosts.candidate.changed; ChangedHostRoot = $hostReceipts.candidate.changed.HostRoot
            ChangedHostSha256 = (Get-FileHash $hosts.candidate.changed).Hash
        }
    }
}
Write-NewJson $planPath $plan

$stageDirectory = Join-Path $destination 'stages'
$workloads = [Collections.Generic.List[object]]::new()
foreach ($workload in @('artifact-cold', 'unchanged', 'declaration-edit', 'generator-change', 'missing-output')) {
    $entry = [ordered]@{ Name = $workload; Samples = if ($workload -eq 'artifact-cold') { 3 } else { 5 } }
    foreach ($variant in @('baseline', 'candidate')) {
        $prepare = New-Stage $stageDirectory $variant $workload Prepare 7200
        $measure = [Collections.Generic.List[string]]::new()
        $measure.Add((New-Stage $stageDirectory $variant $workload Generate 7200))
        if ($workload -eq 'artifact-cold') {
            $measure.Add((New-Stage $stageDirectory $variant $workload Configure 1800))
        }
        $measure.Add((New-Stage $stageDirectory $variant $workload Build 10800))
        $verify = [Collections.Generic.List[string]]::new()
        $verify.Add((New-Stage $stageDirectory $variant $workload Verify 7200))
        if ($workload -in @('declaration-edit', 'generator-change')) {
            $verify.Add((New-Stage $stageDirectory $variant $workload Settle 10800))
        }
        $entry[$variant] = [ordered]@{ Prepare = @($prepare); Measure = @($measure); Verify = @($verify) }
    }
    $workloads.Add($entry)
}

$boundFiles = [Collections.Generic.SortedSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($path in @($planPath, $inputManifestPath, $originalHeaderCopy, $changedHeaderCopy, $adapterPath,
        $manifestTool, $ninjaMetricsTool, $exportInventoryTool, $dotnet, $cmake, $ninja, $compiler, $vcvars, $toolchainFile,
        $toolchainStatus, $statusFiles.baseline, $statusFiles.candidate)) { $null = $boundFiles.Add($path) }
foreach ($binding in $fileBindings) { $null = $boundFiles.Add($binding.Path) }
foreach ($variant in @('baseline', 'candidate')) {
    foreach ($generatorHost in @($hosts[$variant].original, $hosts[$variant].changed)) {
        foreach ($file in Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($generatorHost)) -Recurse -File) {
            $null = $boundFiles.Add($file.FullName)
        }
    }
}

$matrixPath = Join-Path $destination 'matrix.json'
Write-NewJson $matrixPath ([ordered]@{
    RepositoryRoot = $candidateRepository
    Scope = $Scope
    DeadlineUtc = $DeadlineUtc.ToUniversalTime().ToString('O')
    MemoryLimitBytes = $MemoryLimitBytes
    MemoryReserveBytes = $MemoryReserveBytes
    InputFiles = @($boundFiles | ForEach-Object { Get-FileBinding $_ })
    Workloads = @($workloads.ToArray())
})

Write-Output "Frozen OCCT benchmark plan: $matrixPath"
if ($HarnessBinding -ceq 'commit:PENDING-FINAL-HARNESS-COMMIT') {
    Write-Warning 'Plan-only placeholder retained. Regenerate with commit:<final harness SHA> before formal execution.'
}
