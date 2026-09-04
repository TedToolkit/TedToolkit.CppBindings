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
    [Parameter(Mandatory)] [string] $BaselineOriginalHost,
    [Parameter(Mandatory)] [string] $BaselineChangedHost,
    [Parameter(Mandatory)] [string] $CandidateOriginalHost,
    [Parameter(Mandatory)] [string] $CandidateChangedHost,
    [Parameter(Mandatory)] [string] $GeneratorChangePatch,
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
        WorkingDirectory = if ($Variant -eq 'baseline') { $baselineRepository } else { $candidateRepository }
        TimeLimitSeconds = $TimeLimitSeconds
    })
    return $path
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
$generatorPatch = Resolve-ExistingPath $GeneratorChangePatch file
if ((Get-FileHash $baselineHeader).Hash -cne (Get-FileHash $candidateHeader).Hash) {
    throw 'The private baseline and candidate declaration headers must start byte-identical.'
}
if ((Get-FileHash $baselineHeader).Hash -ceq (Get-FileHash $changedHeader).Hash) {
    throw 'The declaration-edit input must differ from the original header.'
}

$toolchainFile = Resolve-ExistingPath (Join-Path $toolchainVcpkg 'scripts/buildsystems/vcpkg.cmake') file
$statusFiles = [ordered]@{
    baseline = Resolve-ExistingPath (Join-Path $baselineInput 'installed/vcpkg/status') file
    candidate = Resolve-ExistingPath (Join-Path $candidateInput 'installed/vcpkg/status') file
}
if ((Get-FileHash $statusFiles.baseline).Hash -cne (Get-FileHash $statusFiles.candidate).Hash) {
    throw 'The private input roots must use the same vcpkg status file.'
}

$hosts = [ordered]@{
    baseline = [ordered]@{
        original = Resolve-ExistingPath $BaselineOriginalHost file
        changed = Resolve-ExistingPath $BaselineChangedHost file
    }
    candidate = [ordered]@{
        original = Resolve-ExistingPath $CandidateOriginalHost file
        changed = Resolve-ExistingPath $CandidateChangedHost file
    }
}
foreach ($variant in @('baseline', 'candidate')) {
    if ((Get-FileHash $hosts[$variant].original).Hash -ceq (Get-FileHash $hosts[$variant].changed).Hash) {
        throw "The $variant generator-change host must be a distinct prebuilt immutable binary."
    }
}
$hostDirectories = @(@($hosts.baseline.original, $hosts.baseline.changed, $hosts.candidate.original,
        $hosts.candidate.changed) | ForEach-Object { [IO.Path]::GetDirectoryName($_) })
if (@($hostDirectories | Sort-Object -Unique).Count -ne 4) {
    throw 'Every original and generator-change host must have its own immutable directory.'
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

$planPath = Join-Path $destination 'occt-plan.json'
$fileBindings = [Collections.Generic.List[object]]::new()
foreach ($path in @($inputManifestPath, $originalHeaderCopy, $changedHeaderCopy, $generatorPatch, $adapterPath,
        $manifestTool, $ninjaMetricsTool, $dotnet, $cmake, $ninja, $compiler, $vcvars, $toolchainFile,
        $statusFiles.baseline, $statusFiles.candidate, $hosts.baseline.original, $hosts.baseline.changed,
        $hosts.candidate.original, $hosts.candidate.changed)) {
    $fileBindings.Add((Get-FileBinding $path))
}

$plan = [ordered]@{
    SchemaVersion = 1
    PlanId = $planId
    ProductionAdoptionAuthorized = $false
    BaselineRevision = $baselineRevision
    CandidateBehaviorRevision = $candidateBehaviorRevision
    CandidateHead = $candidateHead
    HarnessBinding = $HarnessBinding
    Scope = $Scope
    Configuration = $Configuration
    Parallelism = $Parallelism
    Triplet = $triplet
    DeclarationHeaderRelativePath = $DeclarationHeaderRelativePath.Replace('\', '/')
    MissingOutputRelativePath = $MissingOutputRelativePath.Replace('\', '/')
    OriginalHeaderFile = $originalHeaderCopy
    ChangedHeaderFile = $changedHeaderCopy
    GeneratorChangePatch = $generatorPatch
    FrozenInputManifest = $inputManifestPath
    FrozenFileBindings = @($fileBindings.ToArray())
    ToolchainVcpkgRoot = $toolchainVcpkg
    ToolchainFile = $toolchainFile
    Tools = [ordered]@{
        DotNet = $dotnet; CMake = $cmake; Ninja = $ninja; Compiler = $compiler; VcVars = $vcvars
        Manifest = $manifestTool; NinjaMetrics = $ninjaMetricsTool
    }
    Variants = [ordered]@{
        baseline = [ordered]@{
            RepositoryRoot = $baselineRepository; ArtifactRoot = $baselineArtifact; InputVcpkgRoot = $baselineInput
            IncludeRoot = $includeRoots.baseline; HeaderPath = $baselineHeader; StatusFile = $statusFiles.baseline
            StatusFileSha256 = (Get-FileHash $statusFiles.baseline).Hash
            OriginalHost = $hosts.baseline.original; OriginalHostSha256 = (Get-FileHash $hosts.baseline.original).Hash
            ChangedHost = $hosts.baseline.changed; ChangedHostSha256 = (Get-FileHash $hosts.baseline.changed).Hash
        }
        candidate = [ordered]@{
            RepositoryRoot = $candidateRepository; ArtifactRoot = $candidateArtifact; InputVcpkgRoot = $candidateInput
            IncludeRoot = $includeRoots.candidate; HeaderPath = $candidateHeader; StatusFile = $statusFiles.candidate
            StatusFileSha256 = (Get-FileHash $statusFiles.candidate).Hash
            OriginalHost = $hosts.candidate.original; OriginalHostSha256 = (Get-FileHash $hosts.candidate.original).Hash
            ChangedHost = $hosts.candidate.changed; ChangedHostSha256 = (Get-FileHash $hosts.candidate.changed).Hash
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
foreach ($path in @($planPath, $inputManifestPath, $originalHeaderCopy, $changedHeaderCopy, $generatorPatch, $adapterPath,
        $manifestTool, $ninjaMetricsTool, $dotnet, $cmake, $ninja, $compiler, $vcvars, $toolchainFile,
        $statusFiles.baseline, $statusFiles.candidate)) { $null = $boundFiles.Add($path) }
foreach ($variant in @('baseline', 'candidate')) {
    foreach ($host in @($hosts[$variant].original, $hosts[$variant].changed)) {
        foreach ($file in Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($host)) -Recurse -File) {
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
