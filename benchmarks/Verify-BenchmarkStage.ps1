$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runner = Join-Path $PSScriptRoot 'Measure-BenchmarkStage.ps1'
$fixture = Join-Path $PSScriptRoot 'fixtures/StageProcess.ps1'
$repository = Split-Path $PSScriptRoot -Parent
$proofRoot = Join-Path $repository ('out/benchmark/stage-proof-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $proofRoot
$deadline = [DateTimeOffset]::UtcNow.AddMinutes(5).ToString('O')
$pwshPath = (Get-Process -Id $PID).Path
$argument = 'quoted "value" with spaces, Unicode 测试, and literal $()'

# Exercise cleanup against an archived CIM row whose nullable CreationDate is no longer
# available. The identity captured as the hashtable key is the durable comparison source.
$tokens = $null
$parseErrors = $null
$stageAst = [Management.Automation.Language.Parser]::ParseFile(
    $runner, [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The stage runner could not be parsed for cleanup verification.' }
$cleanupAst = @($stageAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -ceq 'Stop-ObservedDescendants'
}, $true))
if ($cleanupAst.Count -ne 1) { throw 'The descendant cleanup function is missing or ambiguous.' }
. ([scriptblock]::Create($cleanupAst[0].Extent.Text))
$cleanupChild = Start-Process -FilePath $pwshPath -ArgumentList @(
    '-NoProfile', '-File', $fixture, '-Mode', 'sleep', '-Value', '') -PassThru -WindowStyle Hidden
try {
    $cleanupIdentity = "$($cleanupChild.Id):$(([DateTimeOffset] $cleanupChild.StartTime).ToUnixTimeMilliseconds())"
    $archived = @{
        $cleanupIdentity = [pscustomobject]@{ ProcessId = $cleanupChild.Id; CreationDate = $null }
    }
    Stop-ObservedDescendants -RootId $PID -Known $archived
    if (-not $cleanupChild.WaitForExit(5000)) {
        throw 'Cleanup did not terminate the child represented by a nullable archived row.'
    }
}
finally {
    if (-not $cleanupChild.HasExited) { $cleanupChild.Kill($true) }
    $cleanupChild.Dispose()
}

$reusedPidChild = Start-Process -FilePath $pwshPath -ArgumentList @(
    '-NoProfile', '-File', $fixture, '-Mode', 'sleep', '-Value', '') -PassThru -WindowStyle Hidden
try {
    $differentIdentity = "$($reusedPidChild.Id):$((([DateTimeOffset] $reusedPidChild.StartTime).ToUnixTimeMilliseconds()) - 1)"
    $stale = @{
        $differentIdentity = [pscustomobject]@{ ProcessId = $reusedPidChild.Id; CreationDate = $null }
    }
    Stop-ObservedDescendants -RootId $PID -Known $stale
    if ($reusedPidChild.HasExited) {
        throw 'Cleanup terminated a process whose captured identity did not match.'
    }
}
finally {
    if (-not $reusedPidChild.HasExited) {
        $reusedPidChild.Kill($true)
        $null = $reusedPidChild.WaitForExit(5000)
    }
    $reusedPidChild.Dispose()
}

$scenarios = @(
    @{ Name = 'success'; Mode = 'echo'; Limit = 20; Memory = 2GB; Expected = $null },
    @{ Name = 'failure'; Mode = 'failure'; Limit = 20; Memory = 2GB; Expected = '*exited with code 17*' },
    @{ Name = 'timeout'; Mode = 'sleep'; Limit = 2; Memory = 2GB; Expected = '*time budget was exceeded*' },
    @{ Name = 'memory'; Mode = 'sleep'; Limit = 20; Memory = 1; Expected = '*memory budget was exceeded*' },
    @{ Name = 'tree'; Mode = 'tree'; Limit = 3; Memory = 2GB; Expected = '*time budget was exceeded*' }
)
foreach ($scenario in $scenarios) {
    $specPath = Join-Path $proofRoot ($scenario.Name + '.json')
    $report = Join-Path $proofRoot $scenario.Name
    $arguments = @('-NoProfile', '-File', $fixture, '-Mode', $scenario.Mode)
    $arguments += @('-Value', $argument)
    $specification = [ordered]@{
        Label = 'Harness verification only: ' + $scenario.Name
        Executable = $pwshPath
        Arguments = $arguments
        WorkingDirectory = $proofRoot
        DeadlineUtc = $deadline
        TimeLimitSeconds = $scenario.Limit
        MemoryLimitBytes = $scenario.Memory
    }
    if ($scenario.Name -eq 'success') {
        $specification['PreMeasurementValidation'] = [ordered]@{
            Executable = $pwshPath
            Arguments = @('-NoProfile', '-File', $fixture, '-Mode', 'delay', '-Value', '1200')
            WorkingDirectory = $proofRoot
            TimeLimitSeconds = 20
        }
    }
    $specification | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $specPath -Encoding utf8
    $errorText = $null
    try { & $runner -SpecificationPath $specPath -ReportDirectory $report }
    catch { $errorText = $_.Exception.Message }
    if ($null -eq $scenario.Expected -and $null -ne $errorText) { throw $errorText }
    if ($null -ne $scenario.Expected -and $errorText -notlike $scenario.Expected) {
        throw "Unexpected result for $($scenario.Name): $errorText"
    }
    $result = Get-Content -LiteralPath (Join-Path $report 'result.json') -Raw | ConvertFrom-Json
    if ($result.Succeeded -ne ($null -eq $scenario.Expected)) { throw 'Result success state does not match the exit.' }
    if ($result.Command.Arguments[-1] -cne $argument) { throw 'Argument boundaries were not preserved.' }
    if ($result.ProcessElapsedSeconds -le 0) { throw 'The process elapsed measurement is missing.' }
    if ($scenario.Name -eq 'failure' -and $result.ExitCode -ne 17) { throw 'The child exit code was lost.' }
    if ($scenario.Name -eq 'success') {
        if (-not $result.PreMeasurementValidation.Configured -or
            -not $result.PreMeasurementValidation.Succeeded -or
            $result.PreMeasurementValidation.IncludedInMeasuredTime -or
            $result.PreMeasurementValidation.ElapsedSeconds -lt 1 -or
            $result.RunnerElapsedSeconds -ge $result.PreMeasurementValidation.ElapsedSeconds -or
            $result.ProcessElapsedSeconds -ge $result.PreMeasurementValidation.ElapsedSeconds -or
            [DateTimeOffset]::Parse($result.StartedAtUtc) -lt
                [DateTimeOffset]::Parse($result.PreMeasurementValidation.CompletedAtUtc)) {
            throw 'Pre-measurement validation contaminated or did not immediately precede the timed child.'
        }
        $output = Get-Content -LiteralPath (Join-Path $report 'stdout.log') -Raw
        if (-not $output.StartsWith($argument) -or $output.Length -lt 1048576) { throw 'Standard output was truncated or arguments changed.' }
        if ((Get-Content -LiteralPath (Join-Path $report 'stderr.log') -Raw).Trim() -ne 'stderr retained') {
            throw 'Standard error was not captured independently.'
        }
    }
    if ($scenario.Name -eq 'tree') {
        if ($result.ObservedProcessCount -lt 2) { throw 'Descendant resource accounting was not exercised.' }
    }
}

$preFailureSpec = Join-Path $proofRoot 'pre-validation-failure.json'
$preFailureReport = Join-Path $proofRoot 'pre-validation-failure'
[ordered]@{
    Label = 'Harness verification only: pre-validation-failure'
    Executable = $pwshPath
    Arguments = @('-NoProfile', '-File', $fixture, '-Mode', 'echo', '-Value', 'must-not-run')
    WorkingDirectory = $proofRoot
    DeadlineUtc = $deadline
    TimeLimitSeconds = 20
    MemoryLimitBytes = 2GB
    PreMeasurementValidation = [ordered]@{
        Executable = $pwshPath
        Arguments = @('-NoProfile', '-File', $fixture, '-Mode', 'failure', '-Value', '')
        WorkingDirectory = $proofRoot
        TimeLimitSeconds = 20
    }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $preFailureSpec -Encoding utf8
$preFailure = $null
try { & $runner -SpecificationPath $preFailureSpec -ReportDirectory $preFailureReport }
catch { $preFailure = $_.Exception.Message }
$preFailureResult = Get-Content -LiteralPath (Join-Path $preFailureReport 'result.json') -Raw | ConvertFrom-Json
if ($preFailure -notlike '*Pre-measurement validation exited with code 17*' -or
    $preFailureResult.PreMeasurementValidation.Succeeded -or
    $preFailureResult.PreMeasurementValidation.ExitCode -ne 17 -or
    $null -ne $preFailureResult.ProcessElapsedSeconds -or
    $null -ne $preFailureResult.StartedAtUtc -or
    (Get-Item -LiteralPath (Join-Path $preFailureReport 'stdout.log')).Length -ne 0) {
    throw 'A failed pre-measurement validation did not block the timed child.'
}

$preHungSpec = Join-Path $proofRoot 'pre-validation-hung.json'
$preHungReport = Join-Path $proofRoot 'pre-validation-hung'
[ordered]@{
    Label = 'Harness verification only: pre-validation-hung'
    Executable = $pwshPath
    Arguments = @('-NoProfile', '-File', $fixture, '-Mode', 'echo', '-Value', 'must-not-run')
    WorkingDirectory = $proofRoot
    DeadlineUtc = $deadline
    TimeLimitSeconds = 20
    MemoryLimitBytes = 2GB
    PreMeasurementValidation = [ordered]@{
        Executable = $pwshPath
        Arguments = @('-NoProfile', '-File', $fixture, '-Mode', 'sleep', '-Value', '')
        WorkingDirectory = $proofRoot
        TimeLimitSeconds = 2
    }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $preHungSpec -Encoding utf8
$preHungFailure = $null
try { & $runner -SpecificationPath $preHungSpec -ReportDirectory $preHungReport }
catch { $preHungFailure = $_.Exception.Message }
$preHungResult = Get-Content -LiteralPath (Join-Path $preHungReport 'result.json') -Raw | ConvertFrom-Json
if ($preHungFailure -notlike '*Pre-measurement validation or experiment time budget was exceeded*' -or
    $preHungResult.PreMeasurementValidation.Succeeded -or
    $preHungResult.PreMeasurementValidation.IncludedInMeasuredTime -or
    $preHungResult.PreMeasurementValidation.ObservedProcessCount -lt 1 -or
    $null -ne $preHungResult.ProcessElapsedSeconds -or
    $null -ne $preHungResult.StartedAtUtc -or
    (Get-Item -LiteralPath (Join-Path $preHungReport 'stdout.log')).Length -ne 0 -or
    (Get-Process -Id $preHungResult.PreMeasurementValidation.ProcessId -ErrorAction SilentlyContinue)) {
    throw 'A hung pre-measurement validation was not bounded before the timed child.'
}

$protectedReport = Join-Path $proofRoot 'success/result.json'
$before = (Get-FileHash -LiteralPath $protectedReport -Algorithm SHA256).Hash
$rejected = $false
try { & $runner -SpecificationPath (Join-Path $proofRoot 'success.json') -ReportDirectory (Join-Path $proofRoot 'success') }
catch { $rejected = $_.Exception.Message -like 'Use a new report directory*' }
if (-not $rejected -or (Get-FileHash -LiteralPath $protectedReport -Algorithm SHA256).Hash -ne $before) {
    throw 'The existing-evidence guard failed.'
}
Write-Output "Stage proof passed: five process scenarios, bounded out-of-band pre-measurement validation, and evidence preservation. Raw evidence: $proofRoot"
