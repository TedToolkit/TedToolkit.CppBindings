#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $SpecificationPath,
    [Parameter(Mandatory)] [string] $ReportDirectory,
    [switch] $PlanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$jsonSnapshots = @{}
. (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')

function Read-JsonSnapshot {
    param([string] $Path)

    $resolved = Resolve-BenchmarkPhysicalPath $Path
    if ($jsonSnapshots.ContainsKey($resolved)) { return $jsonSnapshots[$resolved] }

    $stream = [IO.File]::Open($resolved, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $memory = [IO.MemoryStream]::new()
        try {
            $stream.CopyTo($memory)
            $bytes = $memory.ToArray()
        }
        finally { $memory.Dispose() }
    }
    finally { $stream.Dispose() }

    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $snapshot = [pscustomobject]@{
        Path = $resolved
        Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        Value = $text | ConvertFrom-Json -AsHashtable -DateKind String
    }
    $jsonSnapshots.Add($resolved, $snapshot)
    return $snapshot
}

function Assert-StageArgumentTemplates {
    param([string[]] $Arguments)

    $placeholders = @('{SampleRoot}', '{Sequence}', '{Workload}', '{Variant}', '{Repetition}', '{IsWarmup}')
    foreach ($argument in $Arguments) {
        if ($argument -match '^\{[^{}]+\}$' -and $argument -cnotin $placeholders) {
            throw "Unknown sample placeholder: $argument"
        }
        foreach ($placeholder in $placeholders) {
            if ($argument -cne $placeholder -and $argument.Contains($placeholder, [StringComparison]::Ordinal)) {
                throw "Sample placeholders must occupy a whole stage argument: $argument"
            }
        }
    }
}

function Read-StageGroup {
    param($Paths, [string] $Group)

    if ($Paths -isnot [array] -or $Paths.Count -eq 0) {
        throw "Every workload variant requires a nonempty $Group stage array."
    }
    foreach ($path in $Paths) {
        $snapshot = Read-JsonSnapshot $path
        $value = $snapshot.Value
        foreach ($member in @('Executable', 'Arguments', 'WorkingDirectory', 'TimeLimitSeconds')) {
            if (-not $value.ContainsKey($member)) { throw "Missing stage member: $member" }
        }
        if ($value.Arguments -isnot [array] -or
            @($value.Arguments | Where-Object { $_ -isnot [string] }).Count -gt 0) {
            throw 'Stage arguments must be a JSON string array.'
        }
        Assert-StageArgumentTemplates $value.Arguments
        if ($value.TimeLimitSeconds -isnot [long] -or $value.TimeLimitSeconds -lt 1 -or
            $value.TimeLimitSeconds -gt 43200) { throw 'Invalid stage time limit.' }
        $command = Get-Command $value.Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
        $value.Executable = Resolve-BenchmarkPhysicalPath $command.Source
        $value.WorkingDirectory = Resolve-BenchmarkPhysicalPath $value.WorkingDirectory
        [pscustomobject]@{
            Path = $snapshot.Path
            Sha256 = $snapshot.Sha256
            Specification = $value
        }
    }
}

function Assert-Bindings {
    param([hashtable] $Bindings)

    foreach ($path in $Bindings.Keys) {
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $Bindings[$path]) {
            throw "Input changed; rebaseline before sampling: $path"
        }
    }
}

function Expand-StageArguments {
    param([string[]] $Arguments, $Entry, [string] $SampleRoot)

    $values = @{
        '{SampleRoot}' = $SampleRoot
        '{Sequence}' = $Entry.Sequence.ToString([Globalization.CultureInfo]::InvariantCulture)
        '{Workload}' = $Entry.Workload
        '{Variant}' = $Entry.Variant
        '{Repetition}' = $Entry.Repetition.ToString([Globalization.CultureInfo]::InvariantCulture)
        '{IsWarmup}' = ([bool] $Entry.IsWarmup).ToString().ToLowerInvariant()
    }
    Assert-StageArgumentTemplates $Arguments
    foreach ($argument in $Arguments) {
        if ($values.ContainsKey($argument)) {
            $values[$argument]
            continue
        }
        $argument
    }
}

function Write-Evidence {
    param([string] $Path, $Value)

    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 20))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes) }
    finally { $stream.Dispose() }
}

function Invoke-Sample {
    param($Entry, $Variant)

    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The shared experiment deadline has expired.' }
    Assert-Bindings $bindings
    $sampleRoot = Join-Path $destination ('{0:D3}-{1}-{2}' -f $Entry.Sequence, $Entry.Workload, $Entry.Variant)
    $null = New-Item -ItemType Directory -Path $sampleRoot
    & $environmentRunner -RepositoryRoot $repository -ReportPath (Join-Path $sampleRoot 'environment-before.json') `
        -ArtifactProbePath $artifactProbe -ExpectedArtifactVolumeIdentity $artifactVolume
    $before = Get-Content -LiteralPath (Join-Path $sampleRoot 'environment-before.json') -Raw | ConvertFrom-Json
    if (-not $before.ResourcePreflightPassed -or
        $before.Memory.FreeBytes -lt ($spec.MemoryLimitBytes + $spec.MemoryReserveBytes)) {
        throw 'The sample cannot fit its declared memory budget and reserve.'
    }
    $phaseResults = [Collections.Generic.List[object]]::new()
    foreach ($group in @('Prepare', 'Measure', 'Verify')) {
        $index = 0
        foreach ($stage in $Variant[$group]) {
            if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The shared experiment deadline has expired.' }
            Assert-Bindings $bindings
            $phaseName = '{0}-{1:D2}' -f $group, $index++
            $phase = $stage.Specification.Clone()
            $phase.Label = "$($Entry.Workload)/$($Entry.Variant)/$($Entry.Repetition)/$phaseName"
            $phase.Arguments = @(Expand-StageArguments $phase.Arguments $Entry $sampleRoot)
            $phase.DeadlineUtc = $deadline.ToString('O')
            $phase.MemoryLimitBytes = $spec.MemoryLimitBytes
            $phasePath = Join-Path $sampleRoot ($phaseName + '.json')
            Write-Evidence $phasePath $phase
            $phaseReport = Join-Path $sampleRoot $phaseName
            & $stageRunner -SpecificationPath $phasePath -ReportDirectory $phaseReport
            $result = Get-Content -LiteralPath (Join-Path $phaseReport 'result.json') -Raw | ConvertFrom-Json
            if (-not $result.Succeeded -or $null -eq $result.ProcessElapsedSeconds -or
                $result.ProcessElapsedSeconds -lt 0) { throw "Invalid or failed phase result: $phaseName" }
            $phaseResults.Add([pscustomobject]@{
                Group = $group; Report = $phaseReport; Seconds = $result.ProcessElapsedSeconds
            })
        }
    }
    Assert-Bindings $bindings
    & $environmentRunner -RepositoryRoot $repository -ReportPath (Join-Path $sampleRoot 'environment-after.json') `
        -ArtifactProbePath $artifactProbe -ExpectedArtifactVolumeIdentity $artifactVolume
    $after = Get-Content -LiteralPath (Join-Path $sampleRoot 'environment-after.json') -Raw | ConvertFrom-Json
    if (-not $after.ResourcePreflightPassed) { throw 'Post-sample resource preflight failed.' }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The shared experiment deadline has expired.' }
    $measurement = [pscustomobject]@{
        Sequence = $Entry.Sequence; Workload = $Entry.Workload; Variant = $Entry.Variant
        Repetition = $Entry.Repetition; IsWarmup = $Entry.IsWarmup
        MeasuredSeconds = ($phaseResults | Where-Object Group -eq Measure | Measure-Object Seconds -Sum).Sum
        Phases = @($phaseResults.ToArray())
    }
    Write-Evidence (Join-Path $sampleRoot 'sample.json') $measurement
    return $measurement
}

$specSnapshot = Read-JsonSnapshot $SpecificationPath
$specPath = $specSnapshot.Path
$spec = $specSnapshot.Value
foreach ($member in @('RepositoryRoot', 'ArtifactProbePath', 'ArtifactVolumeIdentity', 'Scope',
        'DeadlineUtc', 'MemoryLimitBytes', 'MemoryReserveBytes',
        'InputFiles', 'Workloads')) {
    if (-not $spec.ContainsKey($member)) { throw "Missing matrix member: $member" }
}
if ($spec.Scope -notin @('screening', 'full')) { throw 'Scope must be screening or full.' }
if ($spec.MemoryLimitBytes -isnot [long] -or $spec.MemoryLimitBytes -le 0 -or
    $spec.MemoryReserveBytes -isnot [long] -or $spec.MemoryReserveBytes -le 0) {
    throw 'Explicit positive integer memory budget and reserve are required.'
}
$deadline = [DateTimeOffset]::Parse($spec.DeadlineUtc).ToUniversalTime()
if ($deadline -le [DateTimeOffset]::UtcNow -or $deadline -gt [DateTimeOffset]::UtcNow.AddHours(12)) {
    throw 'Supply the shared, unexpired experiment deadline within 12 hours; never renew it per variant.'
}
$repository = Resolve-BenchmarkPhysicalPath $spec.RepositoryRoot
$destination = Resolve-BenchmarkPhysicalPath $ReportDirectory
if (Test-Path -LiteralPath $destination) { throw 'Use a new matrix report directory; evidence is never overwritten.' }
$artifactProbe = Resolve-BenchmarkPhysicalPath $spec.ArtifactProbePath
$artifactVolume = [string] $spec.ArtifactVolumeIdentity
if ([string]::IsNullOrWhiteSpace($artifactVolume) -or
    (Get-BenchmarkVolumeIdentity $artifactProbe) -cne $artifactVolume -or
    (Get-BenchmarkVolumeIdentity $repository) -cne $artifactVolume -or
    (Get-BenchmarkVolumeIdentity $destination) -cne $artifactVolume) {
    throw 'Matrix repository, artifact probe, and report directory must remain on the frozen physical volume.'
}
$stageRunner = Join-Path $PSScriptRoot 'Measure-BenchmarkStage.ps1'
$environmentRunner = Join-Path $PSScriptRoot 'Get-BenchmarkEnvironment.ps1'
$bindings = @{ $specPath = $specSnapshot.Sha256 }
foreach ($path in @($PSCommandPath, $stageRunner, $environmentRunner)) {
    $bindings[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
if ($spec.InputFiles -isnot [array] -or $spec.InputFiles.Count -eq 0) {
    throw 'Bind source/toolchain/input manifests before sampling.'
}
foreach ($inputFile in $spec.InputFiles) {
    $path = Resolve-BenchmarkPhysicalPath $inputFile.Path
    if ($inputFile.Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Invalid input SHA-256.' }
    if ($bindings.ContainsKey($path) -and $bindings[$path] -cne $inputFile.Sha256.ToUpperInvariant()) {
        throw 'Conflicting input bindings.'
    }
    $bindings[$path] = $inputFile.Sha256.ToUpperInvariant()
}
Assert-Bindings $bindings
$required = @('artifact-cold', 'unchanged', 'declaration-edit', 'generator-change', 'missing-output')
if ($spec.Workloads -isnot [array] -or $spec.Workloads.Count -ne $required.Count) {
    throw 'The matrix must contain exactly the five approved workloads.'
}
$workloads = @{}
$schedule = [Collections.Generic.List[object]]::new()
$workloadIndex = 0
foreach ($workload in $spec.Workloads) {
    if ($workload.Name -cnotin $required -or $workloads.ContainsKey($workload.Name)) {
        throw 'Unknown or duplicate workload.'
    }
    $minimum = if ($workload.Name -eq 'artifact-cold') { 3 } else { 5 }
    if ($workload.Samples -isnot [long] -or $workload.Samples -gt 100 -or
        ($spec.Scope -eq 'screening' -and $workload.Samples -ne 1) -or
        ($spec.Scope -eq 'full' -and $workload.Samples -lt $minimum)) {
        throw "Invalid sample count for $($workload.Name) and $($spec.Scope) scope."
    }
    $variants = @{}
    foreach ($variantName in @('baseline', 'candidate')) {
        $groups = @{}
        foreach ($group in @('Prepare', 'Measure', 'Verify')) {
            $stages = @(Read-StageGroup $workload[$variantName][$group] $group)
            $groups[$group] = $stages
            foreach ($stage in $stages) {
                if ($bindings.ContainsKey($stage.Path) -and $bindings[$stage.Path] -cne $stage.Sha256) {
                    throw "Conflicting stage bindings: $($stage.Path)"
                }
                $bindings[$stage.Path] = $stage.Sha256
            }
        }
        $variants[$variantName] = $groups
    }
    $workloads[$workload.Name] = $variants
    $firstRepetition = if ($spec.Scope -eq 'screening') { 1 } else { 0 }
    for ($repetition = $firstRepetition; $repetition -le $workload.Samples; $repetition++) {
        $order = if ($spec.Scope -eq 'screening') {
            if ($workloadIndex % 2 -eq 0) { @('baseline', 'candidate') } else { @('candidate', 'baseline') }
        }
        elseif ($repetition % 2 -eq 0) { @('baseline', 'candidate') } else { @('candidate', 'baseline') }
        foreach ($variantName in $order) {
            $schedule.Add([pscustomobject]@{
                Sequence = $schedule.Count; Workload = $workload.Name; Variant = $variantName
                Repetition = $repetition; IsWarmup = $spec.Scope -eq 'full' -and $repetition -eq 0
            })
        }
    }
    $workloadIndex++
}
$pathTool = Resolve-BenchmarkPhysicalPath (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')
$bindings[$pathTool] = (Get-FileHash -LiteralPath $pathTool -Algorithm SHA256).Hash
Assert-Bindings $bindings
$null = New-Item -ItemType Directory -Path $destination
Write-Evidence (Join-Path $destination 'plan.json') ([ordered]@{
    SchemaVersion = 1; Scope = $spec.Scope; DeadlineUtc = $deadline.ToString('O')
    ProductionAdoptionAuthorized = $false
    Bindings = $bindings; Schedule = @($schedule.ToArray()); PlanOnly = [bool] $PlanOnly
})
if ($PlanOnly) { Write-Output "Validated matrix plan: $destination"; return }

$samples = [Collections.Generic.List[object]]::new()
$failure = $null
try {
    foreach ($entry in $schedule) {
        # Child tool status messages must not become sample objects.
        $output = @(Invoke-Sample $entry $workloads[$entry.Workload][$entry.Variant])
        $sample = @($output | Where-Object { $_ -is [pscustomobject] -and $_.PSObject.Properties.Name -contains 'MeasuredSeconds' })
        if ($sample.Count -ne 1) { throw 'Expected exactly one verified sample result.' }
        $samples.Add($sample[0])
        Write-Output "Verified sample $($entry.Sequence): $($entry.Workload) / $($entry.Variant)"
    }
}
catch { $failure = $_.Exception.Message }
$statistics = @($samples | Where-Object { -not $_.IsWarmup } |
    Group-Object Workload, Variant | ForEach-Object {
        $values = @($_.Group.MeasuredSeconds | Sort-Object)
        $middle = [int] [Math]::Floor($values.Count / 2)
        $median = if ($values.Count % 2 -eq 1) { $values[$middle] } else { ($values[$middle - 1] + $values[$middle]) / 2 }
        [pscustomobject]@{
            Workload = $_.Group[0].Workload; Variant = $_.Group[0].Variant; Count = $values.Count
            MedianSeconds = $median; MinimumSeconds = $values[0]; MaximumSeconds = $values[-1]
        }
    })
Write-Evidence (Join-Path $destination 'result.json') ([ordered]@{
    SchemaVersion = 1; Succeeded = $null -eq $failure; Failure = $failure
    Scope = $spec.Scope
    ProductionAdoptionAuthorized = $false
    MeetsRecommendationSamplingRequirements = $spec.Scope -eq 'full' -and $null -eq $failure -and
        $samples.Count -eq $schedule.Count
    EvidenceUse = if ($null -ne $failure) { 'IncompleteNoRecommendation' }
        elseif ($spec.Scope -eq 'screening') { 'CorrectnessResourceAndDirectionalFeasibilityOnly' }
        else { 'RecommendationThresholdAssessment' }
    DeadlineUtc = $deadline.ToString('O'); CompletedSamples = $samples.Count
    Bindings = $bindings; Samples = @($samples.ToArray()); Statistics = $statistics
    Limitations = @(
        'Warmups are retained for diagnosis but excluded from statistics. Setup and verification are not timed stages.',
        'Stage sums are not end-to-end elapsed time; inspect individual stage logs and sampled counters.',
        'Pre/post resource snapshots do not prove absence of contention during a sample.',
        'Input-file binding covers declared files only; a pinned manifest still needs a verifier checking its live corpus.',
        'Prepare/Verify commands must prove cache state, workload edits, source/export equality, rewrites and native behavior.',
        'No recommendation is automatic. Full workload confirmation and independent correctness/resource review are required.',
        'Screening results are feasibility evidence only and cannot be evaluated against the 20% improvement or 5% regression recommendation thresholds.',
        'This invocation shares one deadline; a later matrix or resumed experiment must retain the same deadline.'
    )
})
Write-Output "Matrix report: $destination"
if ($null -ne $failure) { throw $failure }
