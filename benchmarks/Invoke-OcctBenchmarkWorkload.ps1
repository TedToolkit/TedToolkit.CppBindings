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

function Assert-WithinRoot {
    param([string] $Path, [string] $Root)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes its isolated benchmark root: $fullPath"
    }
    return $fullPath
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
    $head = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" -c 'core.excludesFile=NUL' `
        -C $VariantPlan.RepositoryRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($head -join '').Trim() -cne $expected) {
        throw "Repository revision changed for $Variant."
    }
    $status = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" -c 'core.excludesFile=NUL' `
        -C $VariantPlan.RepositoryRoot status --porcelain=v1 --untracked-files=all 2>$null)
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

function Invoke-Generation {
    param([hashtable] $VariantPlan, [bool] $ChangedHost)
    $host = if ($ChangedHost) { $VariantPlan.ChangedHost } else { $VariantPlan.OriginalHost }
    $expected = if ($ChangedHost) { $VariantPlan.ChangedHostSha256 } else { $VariantPlan.OriginalHostSha256 }
    Assert-Hash $host $expected
    $prior = [Environment]::GetEnvironmentVariable('VCPKG_ROOT', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('VCPKG_ROOT', $VariantPlan.InputVcpkgRoot, 'Process')
        Invoke-Checked $plan.Tools.DotNet @($host, '--output-root', $generatedRoot)
    }
    finally {
        $value = if ($null -eq $prior) { [NullString]::Value } else { $prior }
        [Environment]::SetEnvironmentVariable('VCPKG_ROOT', $value, 'Process')
    }
}

function Invoke-NativeBuild {
    $previous = Use-CompilerEnvironment
    try {
        Invoke-Checked $plan.Tools.CMake @('--build', $nativeBuildRoot, '--config', $plan.Configuration,
            '--parallel', ([string] $plan.Parallelism))
    }
    finally { Restore-Environment $previous }
}

function Get-Manifest {
    param([string] $Path, [string] $CompareTo)
    $arguments = @('-NoProfile', '-File', $plan.Tools.Manifest, '-Roots', $csharpRoot, $cppRoot,
        '-Files', $unsupportedHeaders, '-ReportPath', $Path)
    if ($CompareTo) { $arguments += @('-CompareTo', $CompareTo) }
    Invoke-Checked (Get-Process -Id $PID).Path $arguments
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
foreach ($binding in $plan.FrozenFileBindings) { Assert-Hash $binding.Path $binding.Sha256 }
if ($plan.Variants.baseline.ArtifactRoot.Length -ne $plan.Variants.candidate.ArtifactRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot.Length -ne $plan.Variants.candidate.InputVcpkgRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot -or
    $plan.Variants.candidate.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot) {
    throw 'Frozen path-isolation or equal-length guarantees changed.'
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
        Assert-RepositoryRevision $variantPlan
        Assert-FrozenInputs $variantPlan original
        if ($Workload -eq 'artifact-cold') {
            $children = @(Get-ChildItem -LiteralPath $variantPlan.ArtifactRoot -Force |
                Where-Object Name -cne '.occt-benchmark-root.json')
            if (@($children | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
                throw 'Refusing to clean an artifact root containing reparse points.'
            }
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
            Invoke-Checked $plan.Tools.CMake @('--fresh', '-G', 'Ninja Multi-Config', '-Wno-unused-cli',
                '-S', $cppRoot, '-B', $nativeBuildRoot, "-DCMAKE_MAKE_PROGRAM=$($plan.Tools.Ninja)",
                "-DCMAKE_CXX_COMPILER=$($plan.Tools.Compiler)", "-DCMAKE_TOOLCHAIN_FILE=$($plan.ToolchainFile)",
                "-DVCPKG_TARGET_TRIPLET=$($plan.Triplet)", '-DVCPKG_APPLOCAL_DEPS=OFF')
        }
        finally { Restore-Environment $previous }
    }
    'Build' { Invoke-NativeBuild }
    'Verify' {
        Assert-RepositoryRevision $variantPlan
        $headerState = if ($Workload -eq 'declaration-edit') { 'changed' } else { 'original' }
        Assert-FrozenInputs $variantPlan $headerState
        Assert-CompleteArtifacts
        $comparisonPath = if ($Workload -eq 'artifact-cold') { $null } else { $beforeManifest }
        Get-Manifest $afterManifest $comparisonPath
        $after = Read-Json $afterManifest
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
        }
        $metricsArguments = @('-NoProfile', '-File', $plan.Tools.NinjaMetrics, '-LogPath', $ninjaLog,
            '-ReportPath', (Join-Path $sample 'native-metrics.json'))
        if ($Workload -ne 'artifact-cold') { $metricsArguments += @('-BeforeLogPath', $beforeNinja) }
        Invoke-Checked (Get-Process -Id $PID).Path $metricsArguments
        Write-NewJson (Join-Path $sample 'occt-verification.json') ([ordered]@{
            SchemaVersion = 1; Variant = $Variant; Workload = $Workload
            ArtifactManifest = $afterManifest; NinjaMetrics = (Join-Path $sample 'native-metrics.json')
            HarnessBinding = $plan.HarnessBinding; ProductionAdoptionAuthorized = $false
        })
    }
    'Settle' {
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
        if (-not (Read-Json $settledPath).Comparison.EqualContent) {
            throw 'Settlement did not restore the canonical generated content.'
        }
    }
}

Write-Output "$Action completed for $Workload/$Variant."
