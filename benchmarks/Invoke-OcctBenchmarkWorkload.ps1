#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $PlanPath,
    [Parameter(Mandatory)] [ValidateSet('Prepare', 'Generate', 'Configure', 'Build', 'Verify', 'Settle')] [string] $Action,
    [Parameter(Mandatory)] [ValidateSet('baseline', 'candidate')] [string] $Variant,
    [Parameter(Mandatory)] [ValidateSet('artifact-cold', 'unchanged', 'declaration-edit', 'generator-change', 'missing-output')] [string] $Workload,
    [Parameter(Mandatory)] [string] $SampleRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)

function Read-Json {
    param([string] $Path)
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable -DateKind String
}

function Write-NewJson {
    param([string] $Path, $Value)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    $bytes = $utf8.GetBytes(($Value | ConvertTo-Json -Depth 20))
    $stream = [IO.File]::Open($Path, 'CreateNew', 'Write', 'None')
    try { $stream.Write($bytes) }
    finally { $stream.Dispose() }
}

function Assert-Hash {
    param([string] $Path, [string] $Expected)
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $Expected) {
        throw "Frozen benchmark input changed: $Path"
    }
}

function Assert-FrozenBindings {
    foreach ($binding in $plan.FrozenFileBindings) {
        Assert-Hash $binding.Path $binding.Sha256
    }
}

function Assert-WithinRoot {
    param([string] $Path, [string] $Root)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes its isolated benchmark root: $fullPath"
    }
    return $fullPath
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

function Assert-NoNestedReparse {
    param([string] $Root)
    $reparse = Get-ChildItem -LiteralPath $Root -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $reparse) { throw "Benchmark tree contains a nested reparse point: $($reparse.FullName)" }
}

function Assert-IsolatedRoots {
    $roots = [ordered]@{
        baselineRepository = $plan.Variants.baseline.RepositoryRoot
        candidateRepository = $plan.Variants.candidate.RepositoryRoot
        baselineInput = $plan.Variants.baseline.InputVcpkgRoot
        candidateInput = $plan.Variants.candidate.InputVcpkgRoot
        toolchainVcpkg = $plan.ToolchainVcpkgRoot
        baselineArtifact = $plan.Variants.baseline.ArtifactRoot
        candidateArtifact = $plan.Variants.candidate.ArtifactRoot
        canonicalArtifact = $plan.Canonical.ArtifactRoot
        baselineOriginalHost = $plan.Variants.baseline.OriginalHostRoot
        baselineChangedHost = $plan.Variants.baseline.ChangedHostRoot
        candidateOriginalHost = $plan.Variants.candidate.OriginalHostRoot
        candidateChangedHost = $plan.Variants.candidate.ChangedHostRoot
    }
    $names = @($roots.Keys)
    for ($leftIndex = 0; $leftIndex -lt $names.Count; $leftIndex++) {
        $left = [IO.Path]::GetFullPath($roots[$names[$leftIndex]]).TrimEnd('\', '/')
        Assert-NoReparseComponents $left
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $names.Count; $rightIndex++) {
            $right = [IO.Path]::GetFullPath($roots[$names[$rightIndex]]).TrimEnd('\', '/')
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

function Assert-RootMarker {
    param([hashtable] $VariantPlan)
    $markerPath = Join-Path $VariantPlan.ArtifactRoot '.occt-benchmark-root.json'
    $marker = Read-Json $markerPath
    if ($marker.SchemaVersion -ne 1 -or $marker.PlanId -cne $plan.PlanId -or $marker.Variant -cne $Variant) {
        throw 'Artifact-root ownership marker does not match the frozen plan.'
    }
    if ((Get-Item -LiteralPath $VariantPlan.ArtifactRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Artifact roots cannot be reparse points.'
    }
}

function Assert-FrozenInputs {
    param([hashtable] $VariantPlan, [ValidateSet('original', 'changed')] [string] $HeaderState)
    $manifest = Read-Json $plan.FrozenInputManifest
    $reparse = Get-ChildItem -LiteralPath $VariantPlan.IncludeRoot -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $reparse) { throw "Private input tree contains a reparse point: $($reparse.FullName)" }
    $entries = @($manifest.Entries | Where-Object Variant -CEQ $Variant)
    $actual = @(Get-ChildItem -LiteralPath $VariantPlan.IncludeRoot -Recurse -File)
    if ($actual.Count -ne $entries.Count + 1) { throw 'Private include inventory changed; rebaseline.' }
    foreach ($entry in $entries) {
        $path = Assert-WithinRoot (Join-Path $VariantPlan.IncludeRoot $entry.Path) $VariantPlan.IncludeRoot
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item $path).Length -ne $entry.Bytes) {
            throw "Private include inventory changed: $($entry.Path)"
        }
        Assert-Hash $path $entry.Sha256
    }
    $expectedHeaderHash = if ($HeaderState -eq 'original') { $manifest.OriginalHeaderSha256 } else { $manifest.ChangedHeaderSha256 }
    Assert-Hash $VariantPlan.HeaderPath $expectedHeaderHash
    Assert-Hash $VariantPlan.StatusFile $VariantPlan.StatusFileSha256
}

function Assert-RepositoryRevision {
    param([hashtable] $VariantPlan)
    $expected = if ($Variant -eq 'baseline') { $plan.BaselineRevision } else { $plan.CandidateHead }
    $head = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" `
        -C $VariantPlan.RepositoryRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($head -join '').Trim() -cne $expected) {
        throw "Repository revision changed for $Variant."
    }
    $status = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" `
        -C $VariantPlan.RepositoryRoot status --porcelain=v1 --untracked-files=all --ignore-submodules=all 2>$null)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) { throw "Repository is not clean for $Variant." }
}

function Invoke-Checked {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable exited with code $LASTEXITCODE." }
}

function Use-CompilerEnvironment {
    $lines = @(& $env:COMSPEC /d /c "call `"$($plan.Tools.VcVars)`" >nul && set")
    if ($LASTEXITCODE -ne 0) { throw 'The pinned MSVC environment could not be initialized.' }
    $previous = @{}
    foreach ($line in $lines) {
        if ($line -match '^([^=]+)=(.*)$') {
            $previous[$Matches[1]] = [Environment]::GetEnvironmentVariable($Matches[1], 'Process')
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
        }
    }
    return $previous
}

function Restore-Environment {
    param([hashtable] $Previous)
    foreach ($entry in $Previous.GetEnumerator()) {
        $value = if ($null -eq $entry.Value) { [NullString]::Value } else { $entry.Value }
        [Environment]::SetEnvironmentVariable($entry.Key, $value, 'Process')
    }
}

function Assert-ActiveCompilerEnvironment {
    $environmentBindings = [ordered]@{
        VCToolsInstallDir = 'VCToolsInstallDir'
        WindowsSdkDir = 'WindowsSdkDir'
        WindowsSDKVersion = 'WindowsSDKVersion'
        HostArchitecture = 'VSCMD_ARG_HOST_ARCH'
        TargetArchitecture = 'VSCMD_ARG_TGT_ARCH'
        Include = 'INCLUDE'
        Lib = 'LIB'
        LibPath = 'LIBPATH'
    }
    foreach ($binding in $environmentBindings.GetEnumerator()) {
        if ([Environment]::GetEnvironmentVariable($binding.Value, 'Process') -cne
            $plan.ToolchainSnapshot[$binding.Key]) {
            throw "vcvars-selected toolchain changed: $($binding.Value)"
        }
    }
    $selectedCompiler = [IO.Path]::GetFullPath((Join-Path $plan.ToolchainSnapshot.VCToolsInstallDir 'bin/Hostx64/x64/cl.exe'))
    if (-not $selectedCompiler.Equals($plan.Tools.Compiler, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'VCToolsInstallDir selected a different compiler.'
    }
}

function Invoke-Generation {
    param([hashtable] $VariantPlan, [bool] $ChangedHost)
    $generatorHost = if ($ChangedHost) { $VariantPlan.ChangedHost } else { $VariantPlan.OriginalHost }
    $expected = if ($ChangedHost) { $VariantPlan.ChangedHostSha256 } else { $VariantPlan.OriginalHostSha256 }
    Assert-Hash $generatorHost $expected
    $prior = [Environment]::GetEnvironmentVariable('VCPKG_ROOT', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('VCPKG_ROOT', $VariantPlan.InputVcpkgRoot, 'Process')
        Invoke-Checked $plan.Tools.DotNet @($generatorHost, '--output-root', $generatedRoot)
    }
    finally {
        $value = if ($null -eq $prior) { [NullString]::Value } else { $prior }
        [Environment]::SetEnvironmentVariable('VCPKG_ROOT', $value, 'Process')
    }
}

function Invoke-NativeBuild {
    $previous = Use-CompilerEnvironment
    try {
        Assert-ActiveCompilerEnvironment
        Invoke-Checked $plan.Tools.CMake @('--build', $nativeBuildRoot, '--config', $plan.Configuration,
            '--parallel', ([string] $plan.Parallelism))
    }
    finally { Restore-Environment $previous }
}

function Get-Manifest {
    param([string] $Path, [string] $CompareTo)
    $arguments = @{
        Roots = @($csharpRoot, $cppRoot)
        Files = @($unsupportedHeaders)
        ReportPath = $Path
    }
    if ($CompareTo) { $arguments.CompareTo = $CompareTo }
    $null = & $plan.Tools.Manifest @arguments
}

function Assert-CompleteArtifacts {
    if (@(Get-ChildItem -LiteralPath $csharpRoot -File -Filter '*.cs').Count -eq 0 -or
        @(Get-ChildItem -LiteralPath $cppRoot -File -Filter '*.cpp').Count -eq 0 -or
        -not (Test-Path -LiteralPath $unsupportedHeaders -PathType Leaf)) {
        throw 'Generated OCCT source inventory is incomplete.'
    }
    $library = Join-Path $nativeBuildRoot "$($plan.Configuration)/ted_toolkit_occt.dll"
    if (-not (Test-Path -LiteralPath $library -PathType Leaf) -or (Get-Item $library).Length -eq 0) {
        throw 'The native build did not produce the expected nonempty binding library.'
    }
}

function Assert-ManifestOracle {
    param($Actual, [string] $OraclePath)
    $oracle = Read-Json $OraclePath
    if ($Actual.FileCount -ne $oracle.FileCount -or $Actual.TotalBytes -ne $oracle.TotalBytes) {
        throw 'Generated artifacts differ from the canonical baseline oracle.'
    }
    for ($index = 0; $index -lt $Actual.Files.Count; $index++) {
        $left = $Actual.Files[$index]
        $right = $oracle.Files[$index]
        if ($left.Path -cne $right.Path -or $left.Bytes -ne $right.Bytes -or $left.Sha256 -cne $right.Sha256) {
            throw "Generated artifacts differ from the canonical baseline oracle at $($left.Path)."
        }
    }
}

function Expand-NativeGateArguments {
    param([string] $ArtifactRoot, [string] $Phase)
    $values = @{
        '{ArtifactRoot}' = $ArtifactRoot
        '{GeneratedRoot}' = Join-Path $ArtifactRoot 'generated'
        '{NativeBuildRoot}' = Join-Path $ArtifactRoot 'native-build'
        '{Variant}' = $Variant
        '{Workload}' = $Workload
        '{Phase}' = $Phase
    }
    foreach ($argument in $plan.NativeGate.Arguments) {
        if ($values.ContainsKey($argument)) { $values[$argument] } else { $argument }
    }
}

function Invoke-NativeGate {
    param([string] $ArtifactRoot, [string] $Phase)
    Push-Location $plan.NativeGate.WorkingDirectory
    try { Invoke-Checked $plan.NativeGate.Executable @(Expand-NativeGateArguments $ArtifactRoot $Phase) }
    finally { Pop-Location }
}

function Assert-CanonicalBoundary {
    $manifestPath = Join-Path $sample 'canonical-pre-artifacts.json'
    $canonicalGenerated = Join-Path $plan.Canonical.ArtifactRoot 'generated'
    $jsonOptions = [Text.Json.JsonSerializerOptions]::new()
    $rootsJson = [Text.Json.JsonSerializer]::Serialize([string[]]@(
        (Join-Path $canonicalGenerated 'csharp'), (Join-Path $canonicalGenerated 'cpp')), $jsonOptions)
    $filesJson = [Text.Json.JsonSerializer]::Serialize([string[]]@(
        (Join-Path $canonicalGenerated 'unsupported-headers.txt')), $jsonOptions)
    $arguments = @('-NoProfile', '-File', $plan.Tools.Manifest, '-RootsJson', $rootsJson,
        '-FilesJson', $filesJson, '-ReportPath', $manifestPath)
    Invoke-Checked (Get-Process -Id $PID).Path $arguments
    Assert-ManifestOracle (Read-Json $manifestPath) $plan.Canonical.OriginalManifest
    $exportsPath = Join-Path $sample 'canonical-pre-exports.json'
    Invoke-Checked (Get-Process -Id $PID).Path @('-NoProfile', '-File', $plan.Tools.ExportInventory,
        '-SourcePath', (Join-Path $canonicalGenerated 'cpp/NativeFunctionTable.cpp'),
        '-ReportPath', $exportsPath, '-CompareTo', $plan.Canonical.OriginalExportInventory)
    if (-not (Read-Json $exportsPath).Comparison.EqualOrderedExports) {
        throw 'The immutable canonical export boundary changed.'
    }
}

function Assert-ToolchainSnapshot {
    $previous = Use-CompilerEnvironment
    try {
        Assert-ActiveCompilerEnvironment
        $compilerBv = @(& $plan.Tools.Compiler /Bv /c NUL 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($compilerBv -join "`n") -cne $plan.ToolchainSnapshot.CompilerBv) {
            throw 'The selected compiler /Bv identity changed.'
        }
        $cmakeVersion = @(& $plan.Tools.CMake --version 2>&1)
        $ninjaVersion = @(& $plan.Tools.Ninja --version 2>&1)
        $dotnetInfo = @(& $plan.Tools.DotNet --info 2>&1)
        if (($cmakeVersion -join "`n") -cne $plan.ToolchainSnapshot.CMakeVersion -or
            ($ninjaVersion -join "`n") -cne $plan.ToolchainSnapshot.NinjaVersion -or
            ($dotnetInfo -join "`n") -cne $plan.ToolchainSnapshot.DotNetInfo) {
            throw 'The pinned CMake, Ninja, or dotnet environment changed.'
        }
    }
    finally { Restore-Environment $previous }
}

function Copy-NewFile {
    param([string] $Source, [string] $Destination)
    $stream = [IO.File]::Open($Destination, 'CreateNew', 'Write', 'None')
    try {
        $sourceStream = [IO.File]::Open($Source, 'Open', 'Read', 'Read')
        try { $sourceStream.CopyTo($stream) }
        finally { $sourceStream.Dispose() }
    }
    finally { $stream.Dispose() }
}

$resolvedPlan = (Resolve-Path -LiteralPath $PlanPath).Path
$plan = Read-Json $resolvedPlan
if ($plan.SchemaVersion -ne 1 -or $plan.ProductionAdoptionAuthorized -ne $false) {
    throw 'This adapter accepts only schema-1 experiment plans with no production authority.'
}
if ($plan.BaselineRevision -cne 'e94f10a9bb9490d47363cf43d8ce17600b435b8a' -or
    $plan.CandidateBehaviorRevision -cne '9952e5a76358028c22c8ec215a23d7b82413ad4f') {
    throw 'Benchmark code bindings do not match the approved comparison.'
}
if ($plan.HarnessBinding -ceq 'commit:PENDING-FINAL-HARNESS-COMMIT') {
    throw 'Formal workload execution is disabled until the final harness commit is bound.'
}
if ($plan.HarnessBinding -cne "commit:$($plan.CandidateHead)") {
    throw 'Harness binding and candidate HEAD differ.'
}
if ($plan.FixtureOnly) { throw 'Fixture-only OCCT plans cannot execute workloads.' }
if ($plan.NativeGate.FixtureOnly) { throw 'A fixture-only native boundary gate cannot execute workloads.' }
if ($plan.Variants.baseline.ArtifactRoot.Length -ne $plan.Variants.candidate.ArtifactRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot.Length -ne $plan.Variants.candidate.InputVcpkgRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot -or
    $plan.Variants.candidate.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot) {
    throw 'Frozen path-isolation or equal-length guarantees changed.'
}
if ($plan.Variants.baseline.OriginalHost.Length -ne $plan.Variants.candidate.OriginalHost.Length -or
    $plan.Variants.baseline.ChangedHost.Length -ne $plan.Variants.candidate.ChangedHost.Length) {
    throw 'Measured Console host path shape changed.'
}
Assert-IsolatedRoots
if ((Get-VolumeIdentity $plan.Variants.baseline.ArtifactRoot) -cne $plan.ArtifactVolumeIdentity -or
    (Get-VolumeIdentity $plan.Variants.candidate.ArtifactRoot) -cne $plan.ArtifactVolumeIdentity -or
    (Get-VolumeIdentity $plan.Variants.candidate.RepositoryRoot) -cne $plan.ArtifactVolumeIdentity) {
    throw 'Artifact roots moved away from the physical volume checked by resource preflight.'
}

$variantPlan = $plan.Variants[$Variant]
$sample = [IO.Path]::GetFullPath($SampleRoot)
if (-not (Test-Path -LiteralPath $sample -PathType Container)) { throw 'SampleRoot must already exist.' }
Assert-RootMarker $variantPlan
$generatedRoot = Join-Path $variantPlan.ArtifactRoot 'generated'
$csharpRoot = Join-Path $generatedRoot 'csharp'
$cppRoot = Join-Path $generatedRoot 'cpp'
$nativeBuildRoot = Join-Path $variantPlan.ArtifactRoot 'native-build'
$unsupportedHeaders = Join-Path $generatedRoot 'unsupported-headers.txt'
$beforeManifest = Join-Path $sample 'artifacts-before.json'
$afterManifest = Join-Path $sample 'artifacts-after.json'
$beforeNinja = Join-Path $sample 'ninja-before.log'
$ninjaLog = Join-Path $nativeBuildRoot '.ninja_log'

switch ($Action) {
    'Prepare' {
        # Full binding verification is intentionally outside the measured Generate/Configure/Build actions.
        Assert-FrozenBindings
        Assert-RepositoryRevision $variantPlan
        Assert-FrozenInputs $variantPlan original
        Assert-NoNestedReparse $variantPlan.ArtifactRoot
        Assert-ToolchainSnapshot
        Assert-CanonicalBoundary
        Invoke-NativeGate $plan.Canonical.ArtifactRoot pre
        if ($Workload -eq 'artifact-cold') {
            $children = @(Get-ChildItem -LiteralPath $variantPlan.ArtifactRoot -Force |
                Where-Object Name -cne '.occt-benchmark-root.json')
            foreach ($child in $children) { Remove-Item -LiteralPath $child.FullName -Recurse -Force }
            $null = [IO.Directory]::CreateDirectory($generatedRoot)
            break
        }
        Assert-CompleteArtifacts
        Get-Manifest $beforeManifest $null
        Copy-NewFile $ninjaLog $beforeNinja
        if ($Workload -eq 'declaration-edit') {
            [IO.File]::Copy($plan.ChangedHeaderFile, $variantPlan.HeaderPath, $true)
            Assert-FrozenInputs $variantPlan changed
        }
        elseif ($Workload -eq 'missing-output') {
            $missing = Assert-WithinRoot (Join-Path $generatedRoot $plan.MissingOutputRelativePath) $generatedRoot
            if (-not (Test-Path -LiteralPath $missing -PathType Leaf)) { throw 'Pinned missing-output target does not exist.' }
            Remove-Item -LiteralPath $missing
        }
    }
    'Generate' {
        $changedHost = $Workload -eq 'generator-change'
        Invoke-Generation $variantPlan $changedHost
    }
    'Configure' {
        if ($Workload -ne 'artifact-cold') { throw 'cmake --fresh is allowed only for artifact-cold samples.' }
        $previous = Use-CompilerEnvironment
        try {
            Assert-ActiveCompilerEnvironment
            Invoke-Checked $plan.Tools.CMake @('--fresh', '-G', 'Ninja Multi-Config', '-Wno-unused-cli',
                '-S', $cppRoot, '-B', $nativeBuildRoot, "-DCMAKE_MAKE_PROGRAM=$($plan.Tools.Ninja)",
                "-DCMAKE_CXX_COMPILER=$($plan.Tools.Compiler)", "-DCMAKE_TOOLCHAIN_FILE=$($plan.ToolchainFile)",
                "-DVCPKG_TARGET_TRIPLET=$($plan.Triplet)", '-DVCPKG_APPLOCAL_DEPS=OFF')
        }
        finally { Restore-Environment $previous }
    }
    'Build' { Invoke-NativeBuild }
    'Verify' {
        Assert-FrozenBindings
        Assert-RepositoryRevision $variantPlan
        $headerState = if ($Workload -eq 'declaration-edit') { 'changed' } else { 'original' }
        Assert-FrozenInputs $variantPlan $headerState
        Assert-CompleteArtifacts
        $comparisonPath = if ($Workload -eq 'artifact-cold') { $null } else { $beforeManifest }
        Get-Manifest $afterManifest $comparisonPath
        $after = Read-Json $afterManifest
        $oracleManifest = if ($Workload -eq 'declaration-edit') {
            $plan.Canonical.DeclarationManifest
        }
        else { $plan.Canonical.OriginalManifest }
        Assert-ManifestOracle $after $oracleManifest
        if ($Workload -in @('unchanged', 'generator-change', 'missing-output') -and -not $after.Comparison.EqualContent) {
            throw "$Workload changed generated content unexpectedly."
        }
        if ($Workload -eq 'declaration-edit' -and $after.Comparison.EqualContent) {
            throw 'The representative declaration edit changed no generated content.'
        }
        if ($Variant -eq 'candidate' -and $Workload -in @('unchanged', 'generator-change') -and
            @($after.Comparison.ObservedRewritten).Count -ne 0) {
            throw 'The write-if-changed candidate rewrote unchanged sources.'
        }
        if ($Variant -eq 'baseline' -and $Workload -in @('unchanged', 'generator-change', 'missing-output') -and
            @($after.Comparison.ObservedRewritten).Count -eq 0) {
            throw 'The pinned baseline did not exhibit its expected rewrite behavior.'
        }
        if ($Workload -eq 'missing-output') {
            $missing = Assert-WithinRoot (Join-Path $generatedRoot $plan.MissingOutputRelativePath) $generatedRoot
            if (-not (Test-Path -LiteralPath $missing -PathType Leaf)) { throw 'Generation did not restore the missing output.' }
            if ($Variant -eq 'candidate') {
                $relative = $plan.MissingOutputRelativePath.Substring('cpp/'.Length)
                $expectedRewrite = '1/' + $relative
                $observed = @($after.Comparison.ObservedRewritten)
                if ($observed.Count -ne 1 -or $observed[0] -cne $expectedRewrite) {
                    throw 'The write-if-changed candidate rewrote more than the one restored output.'
                }
            }
        }
        $metricsArguments = @('-NoProfile', '-File', $plan.Tools.NinjaMetrics, '-LogPath', $ninjaLog,
            '-ReportPath', (Join-Path $sample 'native-metrics.json'))
        if ($Workload -ne 'artifact-cold') { $metricsArguments += @('-BeforeLogPath', $beforeNinja) }
        Invoke-Checked (Get-Process -Id $PID).Path $metricsArguments
        $oracleExports = if ($Workload -eq 'declaration-edit') {
            $plan.Canonical.DeclarationExportInventory
        }
        else { $plan.Canonical.OriginalExportInventory }
        $exportReport = Join-Path $sample 'exports-after.json'
        Invoke-Checked (Get-Process -Id $PID).Path @('-NoProfile', '-File', $plan.Tools.ExportInventory,
            '-SourcePath', (Join-Path $cppRoot 'NativeFunctionTable.cpp'), '-ReportPath', $exportReport,
            '-CompareTo', $oracleExports)
        if (-not (Read-Json $exportReport).Comparison.EqualOrderedExports) {
            throw 'Generated export order differs from the canonical baseline oracle.'
        }
        Invoke-NativeGate $variantPlan.ArtifactRoot post
        Write-NewJson (Join-Path $sample 'occt-verification.json') ([ordered]@{
            SchemaVersion = 1; Variant = $Variant; Workload = $Workload
            ArtifactManifest = $afterManifest; NinjaMetrics = (Join-Path $sample 'native-metrics.json')
            ExportInventory = $exportReport; NativeGate = 'pre-and-post-passed'
            HarnessBinding = $plan.HarnessBinding; ProductionAdoptionAuthorized = $false
        })
    }
    'Settle' {
        Assert-FrozenBindings
        if ($Workload -notin @('declaration-edit', 'generator-change')) {
            throw 'Only declaration-edit and generator-change samples require settlement.'
        }
        if ($Workload -eq 'declaration-edit') {
            [IO.File]::Copy($plan.OriginalHeaderFile, $variantPlan.HeaderPath, $true)
        }
        Assert-FrozenInputs $variantPlan original
        Invoke-Generation $variantPlan $false
        Invoke-NativeBuild
        $settledPath = Join-Path $sample 'artifacts-settled.json'
        Get-Manifest $settledPath $beforeManifest
        $settled = Read-Json $settledPath
        if (-not $settled.Comparison.EqualContent) {
            throw 'Settlement did not restore the canonical generated content.'
        }
        Assert-ManifestOracle $settled $plan.Canonical.OriginalManifest
    }
}

Write-Output "$Action completed for $Workload/$Variant."
