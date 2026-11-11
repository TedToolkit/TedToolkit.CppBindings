#Requires -Version 7.4
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$releaseScript = Join-Path $PSScriptRoot 'ReleasePackages.ps1'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../out'))
$fixture = Join-Path $temporaryRoot ('release-proof-' + [guid]::NewGuid().ToString('N'))
$cohort = Join-Path $fixture 'release'
$commit = 'a' * 40
$version = '2026.11.10'
$lanes = @{ 'win-x64' = 'Windows'; 'linux-x64' = 'Linux'; 'osx-arm64' = 'MacOS' }
$pushes = [Collections.Generic.List[string]]::new()

function New-FixturePackage([string] $Path, [string] $Id, [string] $PackageVersion, [string] $NativeRid) {
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $nuspec = $archive.CreateEntry("$Id.nuspec")
        $writer = [IO.StreamWriter]::new($nuspec.Open())
        try {
            $writer.Write("<package><metadata><id>$Id</id><version>$PackageVersion</version></metadata></package>")
        }
        finally {
            $writer.Dispose()
        }
        if ($NativeRid) {
            $null = $archive.CreateEntry("runtimes/$NativeRid/native/test-fixture")
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ReleaseRejected([string] $Reason) {
    $previousPushes = $pushes.Count
    try {
        & $releaseScript -Mode Publish -Directory $cohort -Commit $commit -Version $version | Out-Null
        throw 'Publication unexpectedly succeeded.'
    }
    catch {
        if ($_.Exception.Message -notlike $Reason) {
            throw
        }
    }
    if ($pushes.Count -ne $previousPushes) {
        throw 'The publisher ran for an invalid release.'
    }
}

function Invoke-FixturePublisher {
    if ($args[0] -ne 'nuget' -or $args[1] -ne 'push' -or '--skip-duplicate' -cnotin $args) {
        throw 'Unexpected publisher command.'
    }
    $pushes.Add($args[2])
    $global:LASTEXITCODE = 0
}

Set-Alias dotnet Invoke-FixturePublisher
$publisherEnvironment = @{
    GITHUB_ACTIONS = 'true'
    GITHUB_REF = 'refs/heads/main'
    GITHUB_SHA = $commit
    NUGET_SOURCE = 'fixture'
    NUGET_API_KEY = 'fixture'
}
$originalEnvironment = @{}
try {
    foreach ($name in $publisherEnvironment.Keys) {
        $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $publisherEnvironment[$name])
    }

    foreach ($lane in $lanes.Keys) {
        $laneFolder = Join-Path $cohort $lane
        $null = New-Item -ItemType Directory -Path $laneFolder -Force
        $projects = @(& $releaseScript -Mode Projects -Lane $lane)
        foreach ($project in $projects) {
            $id = [IO.Path]::GetFileNameWithoutExtension($project)
            $nativeRid = if ($id.EndsWith(".$($lanes[$lane])", [StringComparison]::Ordinal)) { $lane } else { '' }
            New-FixturePackage (Join-Path $laneFolder "$id.$version.nupkg") $id $version $nativeRid
        }
        & $releaseScript -Mode Record -Lane $lane -Directory $laneFolder -Commit $commit -Version $version
    }

    & $releaseScript -Mode Validate -Directory $cohort -Commit $commit -Version $version
    & $releaseScript -Mode Publish -Directory $cohort -Commit $commit -Version $version
    if ($pushes.Count -ne 27 -or @($pushes | Select-Object -Unique).Count -ne 27) {
        throw 'The complete release did not pass exactly 27 distinct package paths to the publisher.'
    }

    $env:GITHUB_REF = 'refs/heads/development'
    Assert-ReleaseRejected '*requires a GitHub Actions run for the verified main commit*'
    $env:GITHUB_REF = 'refs/heads/main'

    $linuxFolder = Join-Path $cohort 'linux-x64'
    $id = 'TedToolkit.CppBindings.Occt.Linux'
    $packagePath = Join-Path $linuxFolder "$id.$version.nupkg"
    $backup = Join-Path $fixture 'original.nupkg'
    Move-Item -LiteralPath $packagePath -Destination $backup
    Assert-ReleaseRejected '*Missing release entry*'
    Copy-Item -LiteralPath $backup -Destination $packagePath

    $duplicatePath = Join-Path $linuxFolder 'duplicate.nupkg'
    Copy-Item -LiteralPath $backup -Destination $duplicatePath
    Assert-ReleaseRejected '*Unexpected release entry*'
    Remove-Item -LiteralPath $duplicatePath

    Remove-Item -LiteralPath $packagePath
    New-FixturePackage $packagePath $id '2026.11.11' 'linux-x64'
    Assert-ReleaseRejected '*has version*expected*'
    Remove-Item -LiteralPath $packagePath
    New-FixturePackage $packagePath $id $version 'osx-arm64'
    Assert-ReleaseRejected '*has no native assets for*'
    Copy-Item -LiteralPath $backup -Destination $packagePath -Force

    $archive = [IO.Compression.ZipFile]::Open($packagePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $null = $archive.CreateEntry('changed-bytes')
    }
    finally {
        $archive.Dispose()
    }
    Assert-ReleaseRejected '*does not match commit*'
    Copy-Item -LiteralPath $backup -Destination $packagePath -Force

    $manifestPath = Join-Path $linuxFolder 'release-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    Set-Content -LiteralPath $manifestPath -Value $manifest.Replace($commit, ('b' * 40)) -NoNewline
    Assert-ReleaseRejected '*does not match commit*'
    Set-Content -LiteralPath $manifestPath -Value $manifest.Replace($version, '2026.11.11') -NoNewline
    Assert-ReleaseRejected '*does not match commit*'
    Remove-Item -LiteralPath $manifestPath
    Assert-ReleaseRejected '*Missing release entry*'

    Write-Output 'Release proof passed for the complete inventory and rejected incomplete or inconsistent inputs before any push.'
}
finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name])
    }
    if (Test-Path -LiteralPath $fixture) {
        $resolvedFixture = (Resolve-Path -LiteralPath $fixture).ProviderPath
        if ([IO.Path]::GetDirectoryName($resolvedFixture) -cne $temporaryRoot) {
            throw "Release proof fixture resolved outside '$temporaryRoot'."
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
