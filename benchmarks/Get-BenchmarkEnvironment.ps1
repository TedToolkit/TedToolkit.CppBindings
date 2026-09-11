param(
    [Parameter(Mandatory)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory)]
    [string] $ReportPath,

    [Parameter(Mandatory)]
    [string] $ArtifactProbePath,

    [Parameter(Mandatory)]
    [string] $ExpectedArtifactVolumeIdentity,

    [ValidateRange(1, 1024)]
    [int] $MinimumFreeMemoryGiB = 10,

    [ValidateRange(1, 1024)]
    [int] $MinimumFreeDiskGiB = 20
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')

function Invoke-ProbeCommand {
    param([string] $Executable, [string[]] $Arguments)

    $command = Get-Command $Executable -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        return [ordered]@{ Available = $false; Reason = 'Not found on PATH' }
    }

    $output = @(& $command.Source @Arguments 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    return [ordered]@{
        Available = $code -eq 0
        Path = $command.Source
        Arguments = $Arguments
        ExitCode = $code
        Output = $output
    }
}

function Test-CompetingBuild {
    param($Process)

    if ($Process.Name -in @('cl.exe', 'clang.exe', 'clang++.exe', 'ninja.exe', 'link.exe', 'cmake.exe')) {
        return $true
    }

    # Persistent IDE/build servers are not proof of an active competing build.
    if ($Process.Name -notin @('dotnet.exe', 'MSBuild.exe')) { return $false }
    $commandLine = [string] $Process.CommandLine
    if ($commandLine -match '(?i)(/nodemode:|VBCSCompiler|Roslyn.Worker|OmniSharp)') { return $false }
    return $commandLine -match '(?i)(\b(build|run|test|publish|pack)\b|MSBuild\.dll|\.slnx?\b|\.csproj\b)'
}

if (-not $IsWindows) { throw 'This experiment currently requires Windows.' }
$root = Resolve-BenchmarkPhysicalPath $RepositoryRoot
$report = Resolve-BenchmarkPhysicalPath $ReportPath
$artifactProbe = Resolve-BenchmarkPhysicalPath $ArtifactProbePath
if (Test-Path -LiteralPath $report) { throw "Refusing to overwrite an existing report: $report" }
if ([string]::IsNullOrWhiteSpace($ExpectedArtifactVolumeIdentity) -or
    (Get-BenchmarkVolumeIdentity $artifactProbe) -cne $ExpectedArtifactVolumeIdentity -or
    (Get-BenchmarkVolumeIdentity $report) -cne $ExpectedArtifactVolumeIdentity) {
    throw 'The explicit artifact probe and environment report must use the frozen physical volume.'
}

$revision = @(& git -C $root rev-parse HEAD)
if ($LASTEXITCODE -ne 0) { throw 'The baseline Git revision could not be read.' }
$status = @(& git -C $root status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'The baseline working-tree status could not be read.' }
$submodules = @(& git -C $root submodule status --recursive)
if ($LASTEXITCODE -ne 0) { throw 'The native/submodule revision inventory could not be read.' }

$os = Get-CimInstance Win32_OperatingSystem
$cpu = @(Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors)
$driveId = [IO.Path]::GetPathRoot($artifactProbe).TrimEnd('\')
if ($driveId -notmatch '^[A-Za-z]:$') { throw 'Use a local drive for benchmark artifacts.' }
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$driveId'"
if ($null -eq $disk) { throw 'The artifact drive could not be measured.' }
$competing = @(Get-CimInstance Win32_Process | Where-Object { Test-CompetingBuild $_ } |
    Select-Object ProcessId, ParentProcessId, Name, CreationDate)

$tools = [ordered]@{
    Dotnet = Invoke-ProbeCommand 'dotnet' @('--info')
    CMake = Invoke-ProbeCommand 'cmake' @('--version')
    NinjaOnPath = Invoke-ProbeCommand 'ninja' @('--version')
    Clang = Invoke-ProbeCommand 'clang' @('--version')
}
$blockers = [Collections.Generic.List[string]]::new()
if ([long] $os.FreePhysicalMemory * 1KB -lt $MinimumFreeMemoryGiB * 1GB) {
    $blockers.Add('Insufficient free physical memory for the selected screening budget.')
}
if ([long] $disk.FreeSpace -lt $MinimumFreeDiskGiB * 1GB) {
    $blockers.Add('Insufficient free disk space for isolated artifacts.')
}
if ($competing.Count -gt 0) { $blockers.Add('Potential competing builds were observed; no timing sample is valid yet.') }
if (-not $tools.Dotnet.Available -or -not $tools.CMake.Available) {
    $blockers.Add('The required .NET SDK or CMake is unavailable.')
}

$snapshot = [ordered]@{
    SchemaVersion = 1
    CapturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Repository = $root
    Revision = $revision[0]
    WorkingTreeStatus = $status
    SubmoduleRevisions = $submodules
    OperatingSystem = [ordered]@{ Name = $os.Caption; Version = $os.Version }
    Cpu = $cpu
    Memory = [ordered]@{
        TotalBytes = [long] $os.TotalVisibleMemorySize * 1KB
        FreeBytes = [long] $os.FreePhysicalMemory * 1KB
        MinimumFreeBytes = $MinimumFreeMemoryGiB * 1GB
    }
    ArtifactDrive = [ordered]@{
        ProbePath = $artifactProbe
        VolumeIdentity = $ExpectedArtifactVolumeIdentity
        Id = $driveId
        SizeBytes = [long] $disk.Size
        FreeBytes = [long] $disk.FreeSpace
        MinimumFreeBytes = $MinimumFreeDiskGiB * 1GB
    }
    PotentialCompetingBuilds = $competing
    Tools = $tools
    ResourcePreflightPassed = $blockers.Count -eq 0
    Blockers = @($blockers.ToArray())
    Limitations = @(
        'A point-in-time preflight does not certify an idle machine throughout a sample.',
        'Unknown build tools and non-build CPU or I/O contention are not detected.',
        'A dirty tree still needs an exact content binding before comparison.',
        'MSVC, Windows SDK, selected Ninja, OCCT/vcpkg inputs, disk model and stage worker counts must be pinned by the workload driver.',
        'This report contains no benchmark timings and supports no optimization recommendation.'
    )
}
$parent = [IO.Path]::GetDirectoryName($report)
[IO.Directory]::CreateDirectory($parent) | Out-Null
$json = $snapshot | ConvertTo-Json -Depth 8
$stream = [IO.File]::Open($report, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream.Write($bytes, 0, $bytes.Length)
}
finally { $stream.Dispose() }

Write-Output "Environment report: $report"
if ($blockers.Count -gt 0) { throw ($blockers -join ' ') }
Write-Output 'Resource preflight passed. Pin the remaining workload inputs before collecting samples.'
