#Requires -Version 7.5
param([switch] $RealProcess)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$proofRoot = Join-Path $repository ('out/benchmark/matrix-proof-' + [Guid]::NewGuid().ToString('N'))
$toolRoot = Join-Path $proofRoot 'fixture-tools'
$null = New-Item -ItemType Directory -Path $toolRoot
# Test scheduling/control flow without launching builds or faking real benchmark evidence.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Invoke-BenchmarkMatrix.ps1') -Destination $toolRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BenchmarkPath.ps1') -Destination $toolRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/MatrixStage.ps1') -Destination (Join-Path $toolRoot 'Measure-BenchmarkStage.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/MatrixEnvironment.ps1') -Destination (Join-Path $toolRoot 'Get-BenchmarkEnvironment.ps1')
$runner = Join-Path $toolRoot 'Invoke-BenchmarkMatrix.ps1'
. (Join-Path $toolRoot 'BenchmarkPath.ps1')
$pwshPath = (Get-Process -Id $PID).Path
$deadline = [DateTimeOffset]::UtcNow.AddMinutes(10).ToString('O')

function New-MatrixFixture {
    param([string] $Name, [string] $VerifyMode = 'success')

    $root = Join-Path $proofRoot $Name
    $null = New-Item -ItemType Directory -Path $root
    $inputPath = Join-Path $root 'input.txt'
    Set-Content -LiteralPath $inputPath -Value 'pinned fixture input' -Encoding utf8
    $successPath = Join-Path $root 'success-stage.json'
    $verifyPath = Join-Path $root 'verify-stage.json'
    $stage = @{
        Executable = $pwshPath
        Arguments = @('success', '{SampleRoot}', '{Sequence}', '{Workload}', '{Variant}', '{Repetition}', '{IsWarmup}')
        WorkingDirectory = $root; TimeLimitSeconds = 20
    }
    $stage | ConvertTo-Json | Set-Content -LiteralPath $successPath -Encoding utf8
    $stage.Arguments = @($VerifyMode, $inputPath, '{SampleRoot}', '{Sequence}', '{Workload}', '{Variant}',
        '{Repetition}', '{IsWarmup}')
    $stage | ConvertTo-Json | Set-Content -LiteralPath $verifyPath -Encoding utf8
    $variant = @{ Prepare = @($successPath); Measure = @($successPath); Verify = @($verifyPath) }
    $workloads = @('artifact-cold', 'unchanged', 'declaration-edit', 'generator-change', 'missing-output') |
        ForEach-Object {
            @{
                Name = $_; Samples = $(if ($_ -eq 'artifact-cold') { 3 } else { 5 })
                baseline = $variant.Clone(); candidate = $variant.Clone()
            }
        }
    return @{
        Root = $root; Path = (Join-Path $root 'matrix.json'); Report = (Join-Path $root 'report')
        Specification = @{
            RepositoryRoot = $root; ArtifactProbePath = $root
            ArtifactVolumeIdentity = Get-BenchmarkVolumeIdentity $root
            Scope = 'screening'; DeadlineUtc = $deadline
            MemoryLimitBytes = 2GB; MemoryReserveBytes = 1GB
            InputFiles = @(@{ Path = $inputPath; Sha256 = (Get-FileHash -LiteralPath $inputPath).Hash })
            Workloads = @($workloads)
        }
    }
}

function Invoke-Fixture {
    param($Fixture, [string] $ExpectedError, [switch] $PlanOnly)

    $Fixture.Specification | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Fixture.Path -Encoding utf8
    $actualError = $null
    try { & $runner -SpecificationPath $Fixture.Path -ReportDirectory $Fixture.Report -PlanOnly:$PlanOnly | Out-Null }
    catch { $actualError = $_.Exception.Message }
    if ([string]::IsNullOrEmpty($ExpectedError)) {
        if ($null -ne $actualError) { throw $actualError }
    }
    elseif ($actualError -notlike $ExpectedError) { throw "Unexpected fixture result: $actualError" }
}

function Assert-CompleteBindings {
    param($Evidence, $Fixture)

    $expected = @(
        $Fixture.Path,
        $runner,
        (Join-Path $toolRoot 'Measure-BenchmarkStage.ps1'),
        (Join-Path $toolRoot 'Get-BenchmarkEnvironment.ps1'),
        (Join-Path $toolRoot 'BenchmarkPath.ps1'),
        (Join-Path $Fixture.Root 'input.txt'),
        (Join-Path $Fixture.Root 'success-stage.json'),
        (Join-Path $Fixture.Root 'verify-stage.json')
    ) | ForEach-Object { (Resolve-Path -LiteralPath $_).Path } | Sort-Object -Unique
    $actual = @($Evidence.Bindings.PSObject.Properties.Name | Sort-Object -Unique)
    if (@(Compare-Object $expected $actual).Count -ne 0) {
        throw 'Evidence did not bind the complete and exact matrix input set.'
    }
    foreach ($path in $expected) {
        if ($Evidence.Bindings.PSObject.Properties[$path].Value -cne
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash) {
            throw "Evidence did not bind the exact parsed file bytes: $path"
        }
    }
}

$happy = New-MatrixFixture 'happy'
Invoke-Fixture $happy ''
$plan = Get-Content -LiteralPath (Join-Path $happy.Report 'plan.json') -Raw | ConvertFrom-Json -DateKind String
$result = Get-Content -LiteralPath (Join-Path $happy.Report 'result.json') -Raw | ConvertFrom-Json
if (-not $result.Succeeded -or $result.CompletedSamples -ne 56 -or $result.Statistics.Count -ne 10) {
    throw 'The full paired schedule did not finish.'
}
if (@($result.Samples | Where-Object IsWarmup).Count -ne 10) { throw 'Warmup accounting failed.' }
$planBindings = @($plan.Bindings.PSObject.Properties)
$resultBindings = @($result.Bindings.PSObject.Properties)
if ($planBindings.Count -eq 0 -or $resultBindings.Count -ne $planBindings.Count) {
    throw 'The final result did not retain the exact plan bindings.'
}
foreach ($binding in $planBindings) {
    if ($result.Bindings.PSObject.Properties[$binding.Name].Value -cne $binding.Value) {
        throw 'The final result changed a plan binding.'
    }
}
Assert-CompleteBindings $plan $happy
Assert-CompleteBindings $result $happy
$warmupPhase = Get-Content -LiteralPath (Join-Path $happy.Report '000-artifact-cold-baseline/Prepare-00.json') -Raw |
    ConvertFrom-Json
$nextVariantPhase = Get-Content -LiteralPath (Join-Path $happy.Report '001-artifact-cold-candidate/Prepare-00.json') -Raw |
    ConvertFrom-Json
$recordedPhase = Get-Content -LiteralPath (Join-Path $happy.Report '002-artifact-cold-candidate/Prepare-00.json') -Raw |
    ConvertFrom-Json
$expectedWarmupRoot = Join-Path $happy.Report '000-artifact-cold-baseline'
if ($warmupPhase.Arguments[1] -cne $expectedWarmupRoot -or $warmupPhase.Arguments[2] -cne '0' -or
    $warmupPhase.Arguments[3] -cne 'artifact-cold' -or $warmupPhase.Arguments[4] -cne 'baseline' -or
    $warmupPhase.Arguments[5] -cne '0' -or $warmupPhase.Arguments[6] -cne 'true' -or
    $nextVariantPhase.Arguments[1] -ceq $warmupPhase.Arguments[1] -or
    $recordedPhase.Arguments[2] -cne '2' -or $recordedPhase.Arguments[4] -cne 'candidate' -or
    $recordedPhase.Arguments[5] -cne '1' -or $recordedPhase.Arguments[6] -cne 'false') {
    throw 'Sample placeholders were not expanded into unique exact sample context.'
}
foreach ($group in ($plan.Schedule | Group-Object Workload)) {
    $rows = @($group.Group)
    for ($index = 0; $index -lt $rows.Count; $index += 2) {
        $expectedFirst = if (($index / 2) % 2 -eq 0) { 'baseline' } else { 'candidate' }
        if ($rows[$index].Variant -ne $expectedFirst -or $rows[$index].Variant -eq $rows[$index + 1].Variant) {
            throw 'Baseline/candidate order did not alternate.'
        }
    }
}
foreach ($statistic in $result.Statistics) {
    $expectedCount = if ($statistic.Workload -eq 'artifact-cold') { 3 } else { 5 }
    if ($statistic.Count -ne $expectedCount -or $statistic.MedianSeconds -ne 2 -or
        $statistic.MinimumSeconds -ne 2 -or $statistic.MaximumSeconds -ne 2) {
        throw 'Warmups, setup, or verification contaminated the statistics.'
    }
}
foreach ($phase in $result.Samples.Phases) {
    $derived = Get-Content -LiteralPath (Join-Path $phase.Report 'result.json') -Raw | ConvertFrom-Json -DateKind String
    if ($derived.DeadlineUtc -ne $deadline -or $derived.MemoryLimitBytes -ne 2GB) {
        throw 'A phase received a renewed deadline or changed memory budget.'
    }
}
$resultPath = Join-Path $happy.Report 'result.json'
$protectedHash = (Get-FileHash -LiteralPath $resultPath).Hash
Invoke-Fixture $happy 'Use a new matrix report directory*'
if ((Get-FileHash -LiteralPath $resultPath).Hash -ne $protectedHash) { throw 'Evidence was overwritten.' }

$planOnly = New-MatrixFixture 'plan-only'
Invoke-Fixture $planOnly '' -PlanOnly
if (Test-Path -LiteralPath (Join-Path $planOnly.Report 'result.json')) { throw 'Planning produced a measurement result.' }
if (@(Get-ChildItem -LiteralPath $planOnly.Report -Directory).Count -ne 0) { throw 'Planning ran a sample.' }
$planOnlyEvidence = Get-Content -LiteralPath (Join-Path $planOnly.Report 'plan.json') -Raw |
    ConvertFrom-Json -DateKind String
if (-not $planOnlyEvidence.PlanOnly) { throw 'Plan-only evidence did not identify itself.' }
Assert-CompleteBindings $planOnlyEvidence $planOnly

foreach ($case in @('few-warm', 'few-cold', 'duplicate', 'no-verify', 'wrong-hash', 'expired', 'extended',
        'unknown-placeholder', 'embedded-placeholder')) {
    $fixture = New-MatrixFixture $case
    $expected = switch ($case) {
        'few-warm' { $fixture.Specification.Workloads[1].Samples = 4; 'Invalid sample count*' }
        'few-cold' { $fixture.Specification.Workloads[0].Samples = 2; 'Invalid sample count*' }
        'duplicate' { $fixture.Specification.Workloads[1].Name = 'artifact-cold'; 'Unknown or duplicate workload*' }
        'no-verify' { $fixture.Specification.Workloads[0].baseline.Verify = @(); '*nonempty Verify stage array*' }
        'wrong-hash' { $fixture.Specification.InputFiles[0].Sha256 = ('0' * 64); 'Input changed*' }
        'expired' { $fixture.Specification.DeadlineUtc = [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('O'); '*shared, unexpired experiment deadline*' }
        'extended' { $fixture.Specification.DeadlineUtc = [DateTimeOffset]::UtcNow.AddHours(13).ToString('O'); '*shared, unexpired experiment deadline*' }
        'unknown-placeholder' {
            $stage = Get-Content -LiteralPath $fixture.Specification.Workloads[0].baseline.Prepare[0] -Raw |
                ConvertFrom-Json -AsHashtable
            $stage.Arguments = @('success', '{Unknown}')
            $stage | ConvertTo-Json | Set-Content -LiteralPath $fixture.Specification.Workloads[0].baseline.Prepare[0] -Encoding utf8
            'Unknown sample placeholder*'
        }
        'embedded-placeholder' {
            $stage = Get-Content -LiteralPath $fixture.Specification.Workloads[0].baseline.Prepare[0] -Raw |
                ConvertFrom-Json -AsHashtable
            $stage.Arguments = @('success', "prefix-{SampleRoot}")
            $stage | ConvertTo-Json | Set-Content -LiteralPath $fixture.Specification.Workloads[0].baseline.Prepare[0] -Encoding utf8
            'Sample placeholders must occupy a whole stage argument*'
        }
    }
    Invoke-Fixture $fixture $expected -PlanOnly
    if (Test-Path -LiteralPath $fixture.Report) { throw 'Invalid specification created execution artifacts.' }
}
foreach ($case in @('verification-failure', 'changed-input', 'resource-failure')) {
    $mode = switch ($case) { 'verification-failure' { 'failure' }; 'changed-input' { 'mutate' }; default { 'success' } }
    $fixture = New-MatrixFixture $case $mode
    $expected = switch ($case) {
        'verification-failure' { 'Controlled stage failure*' }
        'changed-input' { 'Input changed*' }
        'resource-failure' { '*declared memory budget*' }
    }
    Invoke-Fixture $fixture $expected
    $failed = Get-Content -LiteralPath (Join-Path $fixture.Report 'result.json') -Raw | ConvertFrom-Json
    if ($failed.Succeeded -or $failed.CompletedSamples -ne 0) { throw 'A failed/unchecked sample was accepted.' }
    if (@(Get-ChildItem -LiteralPath $fixture.Report -Directory).Count -ne 1) { throw 'The failing matrix continued sampling.' }
}
if ($RealProcess) {
    # Keep the deterministic readiness fixture, but exercise the actual measured-child boundary.
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Measure-BenchmarkStage.ps1') -Destination (Join-Path $toolRoot 'Measure-BenchmarkStage.ps1') -Force
    $fixture = New-MatrixFixture 'real-process'
    $child = Join-Path $PSScriptRoot 'fixtures/StageProcess.ps1'
    foreach ($stageFile in @('success-stage.json', 'verify-stage.json')) {
        $path = Join-Path $fixture.Root $stageFile
        $stage = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        $mode = if ($stageFile -eq 'success-stage.json') { 'echo' } else { 'failure' }
        $stage.Arguments = @('-NoProfile', '-File', $child, '-Mode', $mode, '-Value', 'Matrix integration fixture only')
        $stage | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8
    }
    Invoke-Fixture $fixture '*exited with code 17*'
    $failed = Get-Content -LiteralPath (Join-Path $fixture.Report 'result.json') -Raw | ConvertFrom-Json
    $failedPhase = Join-Path $fixture.Report '000-artifact-cold-baseline/Verify-00/result.json'
    $phase = Get-Content -LiteralPath $failedPhase -Raw | ConvertFrom-Json
    if ($failed.Succeeded -or $failed.CompletedSamples -ne 0 -or $phase.ExitCode -ne 17 -or
        $phase.ProcessElapsedSeconds -le 0) { throw 'The actual stage failure did not invalidate the sample.' }
    Write-Output 'Real process integration passed: actual process capture and failing verification reject the sample.'
}
Write-Output "Matrix proof passed: paired schedule, exact sample placeholders, retained bindings, warmup exclusion, budget sharing, evidence guard, plan-only, nine invalid plans and three fail-closed runs. Fixture-only evidence: $proofRoot"
