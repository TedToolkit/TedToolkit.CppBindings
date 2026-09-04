#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $CompletionReceiptPath,
    [Parameter(Mandatory)] [string] $SourceRepositoryRoot,
    [Parameter(Mandatory)] [string] $ExpectedSourceRevision,
    [Parameter(Mandatory)] [string] $HostDirectory,
    [Parameter(Mandatory)] [string] $DotNetPath,
    [switch] $FixtureOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)
$consoleProjectRelativePath = 'tests/TedToolkit.CppBindings.Occt.Console/TedToolkit.CppBindings.Occt.Console.csproj'
. (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')

function Invoke-Git {
    param([string[]] $Arguments)
    $output = @(& git -c "safe.directory=$sourceRoot" -C $sourceRoot @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "git failed while publishing a benchmark host: $($Arguments -join ' ')" }
    return $output
}

function Assert-ExactCleanSource {
    $revision = ((Invoke-Git @('rev-parse', 'HEAD')) -join '').Trim()
    $status = @(Invoke-Git @('status', '--porcelain=v1', '--untracked-files=all', '--ignore-submodules=all'))
    if ($revision -cne $ExpectedSourceRevision -or $status.Count -ne 0) {
        throw 'Host publishing requires the exact clean source revision.'
    }
}

function Get-HostManifest {
    $files = [Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $hostRoot -Recurse -File | Sort-Object FullName) {
        $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', 'Read')
        try {
            $files.Add([ordered]@{
                Path = [IO.Path]::GetRelativePath($hostRoot, $file.FullName).Replace('\', '/')
                Bytes = $stream.Length
                Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
            })
        }
        finally { $stream.Dispose() }
    }
    return @($files.ToArray())
}

$receipt = Resolve-BenchmarkPhysicalPath $CompletionReceiptPath
$sourceRoot = Resolve-BenchmarkPhysicalPath $SourceRepositoryRoot
$hostRoot = Resolve-BenchmarkPhysicalPath $HostDirectory
$dotnet = Resolve-BenchmarkPhysicalPath $DotNetPath
if ($ExpectedSourceRevision -notmatch '^[0-9a-f]{40}$') { throw 'ExpectedSourceRevision must be a lowercase full Git SHA.' }
if (Test-Path -LiteralPath $receipt) { throw 'Refusing to overwrite a host publish completion receipt.' }
if (Test-Path -LiteralPath $hostRoot) { throw 'HostDirectory must be absent so the publish output is provably fresh.' }
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'DotNetPath must be an existing executable file.' }
if (Test-BenchmarkPathWithin $hostRoot $sourceRoot -OrEqual) {
    throw 'HostDirectory must be outside the source repository.'
}
if ((Test-BenchmarkPathWithin $receipt $hostRoot -OrEqual) -or
    (Test-BenchmarkPathWithin $receipt $sourceRoot -OrEqual)) {
    throw 'The completion receipt must be outside both the fresh host and source repository.'
}

$project = Resolve-BenchmarkPhysicalPath (Join-Path $sourceRoot $consoleProjectRelativePath)
if (-not (Test-BenchmarkPathWithin $project $sourceRoot) -or
    -not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw 'The exact OCCT Console project is missing from the source repository.'
}
Assert-ExactCleanSource

$arguments = @(
    'publish', $project,
    '--configuration', 'Release',
    '--framework', 'net10.0',
    '--no-restore',
    '--output', $hostRoot,
    '--nologo'
)
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($hostRoot))
$null = [IO.Directory]::CreateDirectory($hostRoot)
if (@(Get-ChildItem -LiteralPath $hostRoot -Force).Count -ne 0) {
    throw 'The newly reserved HostDirectory was not empty.'
}

$start = [DateTimeOffset]::UtcNow
$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $dotnet
$startInfo.WorkingDirectory = $sourceRoot
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.StandardOutputEncoding = $utf8
$startInfo.StandardErrorEncoding = $utf8
foreach ($argument in $arguments) { $null = $startInfo.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
try {
    if (-not $process.Start()) { throw 'The exact dotnet publish process did not start.' }
    $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
    $standardErrorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
    $standardError = $standardErrorTask.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
}
finally { $process.Dispose() }
$completed = [DateTimeOffset]::UtcNow
if ($exitCode -ne 0) {
    throw "The exact dotnet publish failed with exit code $exitCode. The fresh host directory is retained."
}

Assert-ExactCleanSource
$hostFiles = @(Get-HostManifest)
if ($hostFiles.Count -lt 3) { throw 'The fresh publish did not produce a complete Console host.' }
$entryPoint = Join-Path $hostRoot 'TedToolkit.CppBindings.Occt.Console.dll'
try { $assembly = [Reflection.AssemblyName]::GetAssemblyName($entryPoint) }
catch { throw 'The fresh publish did not produce a valid OCCT Console assembly.' }
if ($assembly.Name -cne 'TedToolkit.CppBindings.Occt.Console') {
    throw 'The fresh publish produced the wrong managed assembly.'
}

$wrapper = Resolve-BenchmarkPhysicalPath $PSCommandPath
$document = [ordered]@{
    SchemaVersion = 1
    ReceiptKind = 'occt-console-host-publish'
    FixtureOnly = [bool] $FixtureOnly
    Succeeded = $true
    ExitCode = $exitCode
    StartedAtUtc = $start.ToString('O')
    CompletedAtUtc = $completed.ToString('O')
    FreshHostDirectory = $true
    SourceRepositoryRoot = $sourceRoot
    SourceRevision = $ExpectedSourceRevision
    SourceCleanBeforeAndAfter = $true
    ProjectRelativePath = $consoleProjectRelativePath
    ProjectPath = $project
    ProjectSha256 = (Get-FileHash -LiteralPath $project -Algorithm SHA256).Hash
    DotNetPath = $dotnet
    DotNetSha256 = (Get-FileHash -LiteralPath $dotnet -Algorithm SHA256).Hash
    PublishWrapperPath = $wrapper
    PublishWrapperSha256 = (Get-FileHash -LiteralPath $wrapper -Algorithm SHA256).Hash
    Command = [ordered]@{ Executable = $dotnet; Arguments = $arguments; WorkingDirectory = $sourceRoot }
    Output = [ordered]@{ StandardOutput = $standardOutput; StandardError = $standardError }
    HostDirectory = $hostRoot
    HostEntryPointRelativePath = 'TedToolkit.CppBindings.Occt.Console.dll'
    HostAssemblyIdentity = $assembly.FullName
    HostFiles = $hostFiles
}
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt))
$bytes = $utf8.GetBytes(($document | ConvertTo-Json -Depth 10))
$stream = [IO.File]::Open($receipt, 'CreateNew', 'Write', 'None')
try { $stream.Write($bytes) }
finally { $stream.Dispose() }
Write-Output "Fresh Console host publish receipt: $receipt"
