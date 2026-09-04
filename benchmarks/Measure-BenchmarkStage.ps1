#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $SpecificationPath,
    [Parameter(Mandatory)] [string] $ReportDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Process-tree measurement currently requires Windows CIM.' }

function Get-ProcessIdentity {
    param($Row)
    return "$($Row.ProcessId):$(([DateTimeOffset] $Row.CreationDate).ToUnixTimeMilliseconds())"
}

function Read-ProcessTree {
    param([int] $RootId, [hashtable] $Known)

    $rows = @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, CreationDate,
        WorkingSetSize, KernelModeTime, UserModeTime, ReadTransferCount, WriteTransferCount)
    $current = @{}
    foreach ($row in $rows) {
        $identity = Get-ProcessIdentity $row
        if ($row.ProcessId -eq $RootId -or $Known.ContainsKey($identity)) {
            $current[[int] $row.ProcessId] = $row
        }
    }
    do {
        $added = $false
        foreach ($row in $rows) {
            if ($current.ContainsKey([int] $row.ProcessId) -or
                -not $current.ContainsKey([int] $row.ParentProcessId)) { continue }
            if ($row.CreationDate -lt $current[[int] $row.ParentProcessId].CreationDate) { continue }
            $current[[int] $row.ProcessId] = $row
            $added = $true
        }
    } while ($added)

    [long] $workingSet = 0
    foreach ($row in $current.Values) {
        $workingSet += [long] $row.WorkingSetSize
        $Known[(Get-ProcessIdentity $row)] = $row
    }
    return $workingSet
}

function Stop-ObservedDescendants {
    param([int] $RootId, [hashtable] $Known)

    foreach ($row in $Known.Values) {
        if ($row.ProcessId -eq $RootId) { continue }
        $child = Get-Process -Id $row.ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $child) { continue }
        try {
            $created = ([DateTimeOffset] $child.StartTime).ToUnixTimeMilliseconds()
            if ($created -ne ([DateTimeOffset] $row.CreationDate).ToUnixTimeMilliseconds()) { continue }
            if (-not $child.HasExited) {
                $child.Kill($true)
                if (-not $child.WaitForExit(5000)) { throw 'An observed descendant did not terminate.' }
            }
        }
        finally { $child.Dispose() }
    }
}

$specPath = (Resolve-Path -LiteralPath $SpecificationPath).Path
$specHash = (Get-FileHash -LiteralPath $specPath -Algorithm SHA256).Hash
$runnerHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$spec = Get-Content -LiteralPath $specPath -Raw | ConvertFrom-Json -DateKind String
foreach ($name in @('Label', 'Executable', 'Arguments', 'WorkingDirectory', 'DeadlineUtc',
        'TimeLimitSeconds', 'MemoryLimitBytes')) {
    if ($spec.PSObject.Properties.Name -notcontains $name) { throw "Missing specification member: $name" }
}
if ($spec.Arguments -isnot [array]) { throw 'Arguments must be a JSON array.' }
if (@($spec.Arguments | Where-Object { $_ -isnot [string] }).Count -gt 0) {
    throw 'Every argument must be a string.'
}
if ($spec.TimeLimitSeconds -lt 1 -or $spec.TimeLimitSeconds -gt 43200) {
    throw 'The stage time limit must be between 1 second and 12 hours.'
}
if ($spec.MemoryLimitBytes -le 0) { throw 'An explicit positive memory ceiling is required.' }
$deadline = [DateTimeOffset]::Parse($spec.DeadlineUtc).ToUniversalTime()
if ($deadline -le [DateTimeOffset]::UtcNow) { throw 'The experiment deadline has expired.' }
if ($deadline -gt [DateTimeOffset]::UtcNow.AddHours(12)) { throw 'The deadline exceeds the 12-hour experiment ceiling.' }
$workDirectory = (Resolve-Path -LiteralPath $spec.WorkingDirectory).Path
$executable = Get-Command $spec.Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
$preValidation = $null
if ($spec.PSObject.Properties.Name -contains 'PreMeasurementValidation') {
    $preValidation = $spec.PreMeasurementValidation
    foreach ($name in @('Executable', 'Arguments', 'WorkingDirectory')) {
        if ($preValidation.PSObject.Properties.Name -notcontains $name) {
            throw "Missing pre-measurement validation member: $name"
        }
    }
    if ($preValidation.Arguments -isnot [array] -or
        @($preValidation.Arguments | Where-Object { $_ -isnot [string] }).Count -gt 0) {
        throw 'Pre-measurement validation arguments must be a JSON string array.'
    }
    $preExecutable = Get-Command $preValidation.Executable -CommandType Application -ErrorAction Stop |
        Select-Object -First 1
    $preWorkDirectory = (Resolve-Path -LiteralPath $preValidation.WorkingDirectory).Path
}
$destination = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Use a new report directory for each stage; existing evidence is never overwritten.' }

# Verify counter access before starting any workload. Failure must not leave an unmeasured child.
$null = Get-CimInstance Win32_Process -Filter "ProcessId=$PID" -Property ProcessId, CreationDate
$null = New-Item -ItemType Directory -Path $destination -ErrorAction Stop
$stdout = $null
$stderr = $null
$stdoutCopy = $null
$stderrCopy = $null
$started = $false
$failure = $null
$exitCode = $null
$processSeconds = $null
$known = @{}
[long] $peakWorkingSet = 0
$samples = 0
$stopwatch = [Diagnostics.Stopwatch]::new()
$preStopwatch = [Diagnostics.Stopwatch]::new()
$preStartedAt = $null
$preCompletedAt = $null
$preExitCode = $null
$preSucceeded = $null -eq $preValidation
$preProcess = $null
$process = [Diagnostics.Process]::new()
$process.StartInfo.FileName = $executable.Source
$process.StartInfo.WorkingDirectory = $workDirectory
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.RedirectStandardError = $true
foreach ($argument in $spec.Arguments) { $process.StartInfo.ArgumentList.Add($argument) }
$startedAt = $null
if ($null -ne $preValidation) {
    $preProcess = [Diagnostics.Process]::new()
    $preProcess.StartInfo.FileName = $preExecutable.Source
    $preProcess.StartInfo.WorkingDirectory = $preWorkDirectory
    $preProcess.StartInfo.UseShellExecute = $false
    $preProcess.StartInfo.CreateNoWindow = $true
    foreach ($argument in $preValidation.Arguments) { $preProcess.StartInfo.ArgumentList.Add($argument) }
}
try {
    $stdout = [IO.File]::Open((Join-Path $destination 'stdout.log'), [IO.FileMode]::CreateNew)
    $stderr = [IO.File]::Open((Join-Path $destination 'stderr.log'), [IO.FileMode]::CreateNew)
    if ($null -ne $preProcess) {
        $preStartedAt = [DateTimeOffset]::UtcNow
        $preStopwatch.Start()
        if (-not $preProcess.Start()) { throw 'The pre-measurement validation process did not start.' }
        $preProcess.WaitForExit()
        $preExitCode = $preProcess.ExitCode
        if ($preExitCode -ne 0) { throw "Pre-measurement validation exited with code $preExitCode." }
        $preStopwatch.Stop()
        $preCompletedAt = [DateTimeOffset]::UtcNow
        $preSucceeded = $true
        if ($preCompletedAt -ge $deadline) { throw 'The experiment deadline expired during pre-measurement validation.' }
    }
    $startedAt = [DateTimeOffset]::UtcNow
    $stopwatch.Start()
    $started = $process.Start()
    if (-not $started) { throw 'The workload process did not start.' }
    $stdoutCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
    $stderrCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
    while (-not $process.HasExited) {
        if ($stopwatch.Elapsed.TotalSeconds -ge $spec.TimeLimitSeconds -or
            [DateTimeOffset]::UtcNow -ge $deadline) { throw 'The stage or experiment time budget was exceeded.' }
        $workingSet = Read-ProcessTree $process.Id $known
        $samples++
        $peakWorkingSet = [Math]::Max($peakWorkingSet, $workingSet)
        if ($workingSet -gt $spec.MemoryLimitBytes) { throw 'The observed process-tree memory budget was exceeded.' }
        $null = $process.WaitForExit(250)
    }
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $processSeconds = ($process.ExitTime.ToUniversalTime() - $process.StartTime.ToUniversalTime()).TotalSeconds
    if ($exitCode -ne 0) { throw "The workload exited with code $exitCode." }
}
catch { $failure = $_.Exception.Message }
finally {
    try {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) { throw 'The workload did not terminate.' }
        }
        if ($started) {
            # Workloads must not intentionally daemonize; also clean up observed children after failure.
            Stop-ObservedDescendants $process.Id $known
        }
    }
    catch { $failure = "$failure Cleanup failed: $($_.Exception.Message)".Trim() }
    if ($started -and $process.HasExited) {
        $exitCode = $process.ExitCode
        if ($null -eq $processSeconds) {
            $processSeconds = ($process.ExitTime.ToUniversalTime() - $process.StartTime.ToUniversalTime()).TotalSeconds
        }
    }
    $stopwatch.Stop()
    foreach ($copy in @($stdoutCopy, $stderrCopy)) {
        if ($null -eq $copy) { continue }
        try {
            if (-not $copy.Wait(5000)) { throw 'An output pipe remained open after process cleanup.' }
            $null = $copy.GetAwaiter().GetResult()
        }
        catch { $failure = "$failure Log capture failed: $($_.Exception.GetBaseException().Message)".Trim() }
    }
    if ($null -ne $stdout) { $stdout.Dispose() }
    if ($null -ne $stderr) { $stderr.Dispose() }
    if ($null -ne $preProcess) {
        if ($preStopwatch.IsRunning) { $preStopwatch.Stop() }
        if ($null -eq $preCompletedAt) { $preCompletedAt = [DateTimeOffset]::UtcNow }
        $preProcess.Dispose()
    }
    $process.Dispose()
}

[decimal] $cpuTicks = 0
[decimal] $readBytes = 0
[decimal] $writeBytes = 0
foreach ($row in $known.Values) {
    $cpuTicks += [decimal] $row.KernelModeTime + [decimal] $row.UserModeTime
    $readBytes += [decimal] $row.ReadTransferCount
    $writeBytes += [decimal] $row.WriteTransferCount
}
try {
    if ((Get-FileHash -LiteralPath $specPath -Algorithm SHA256).Hash -ne $specHash -or
        (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -ne $runnerHash) {
        throw 'The specification or stage runner changed during measurement; reject this sample.'
    }
}
catch { $failure = "$failure Evidence binding failed: $($_.Exception.Message)".Trim() }
$result = [ordered]@{
    SchemaVersion = 1
    Label = $spec.Label
    SpecificationSha256 = $specHash
    RunnerSha256 = $runnerHash
    Command = [ordered]@{ Executable = $executable.Source; Arguments = $spec.Arguments; WorkingDirectory = $workDirectory }
    StartedAtUtc = if ($null -eq $startedAt) { $null } else { $startedAt.ToString('O') }
    DeadlineUtc = $deadline.ToString('O')
    TimeLimitSeconds = $spec.TimeLimitSeconds
    MemoryLimitBytes = $spec.MemoryLimitBytes
    Succeeded = $null -eq $failure
    Failure = $failure
    ExitCode = $exitCode
    ProcessElapsedSeconds = $processSeconds
    RunnerElapsedSeconds = $stopwatch.Elapsed.TotalSeconds
    SampledPeakTreeWorkingSetBytes = $peakWorkingSet
    SampledTreeCpuSeconds = $cpuTicks / 10000000
    SampledTreeReadTransferBytes = $readBytes
    SampledTreeWriteTransferBytes = $writeBytes
    CounterSamples = $samples
    ObservedProcessCount = $known.Count
    PreMeasurementValidation = [ordered]@{
        Configured = $null -ne $preValidation
        Command = if ($null -eq $preValidation) { $null } else {
            [ordered]@{
                Executable = $preExecutable.Source
                Arguments = $preValidation.Arguments
                WorkingDirectory = $preWorkDirectory
            }
        }
        StartedAtUtc = if ($null -eq $preStartedAt) { $null } else { $preStartedAt.ToString('O') }
        CompletedAtUtc = if ($null -eq $preCompletedAt) { $null } else { $preCompletedAt.ToString('O') }
        ElapsedSeconds = $preStopwatch.Elapsed.TotalSeconds
        Succeeded = $preSucceeded
        ExitCode = $preExitCode
        IncludedInMeasuredTime = $false
    }
    Limitations = @(
        'CIM polling perturbs the workload; process elapsed time excludes post-exit polling delay but not polling contention.',
        'Working sets sum shared pages and miss between-sample peaks; this is not an OS-enforced memory cap.',
        'CPU and transfer counters sum the last observation of each process and miss short-lived or unobserved children.',
        'Transfer counters are process I/O, not physical disk traffic. OS-cache coldness is not established.',
        'The driver must perform readiness checks, bind inputs, share one experiment deadline, and reject competing builds.',
        'Disable build servers and do not daemonize. Unobserved descendants may escape cleanup after their parent exits.',
        'No compiled-TU, rewritten-file, per-file outlier, or correctness measurement is supplied by this process-stage runner.'
    )
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'result.json') -Encoding utf8
Write-Output "Stage report: $destination"
if ($null -ne $failure) { throw $failure }
