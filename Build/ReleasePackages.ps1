#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Projects', 'Record', 'Validate', 'Publish')]
    [string] $Mode,

    [ValidateSet('win-x64', 'linux-x64', 'osx-arm64')]
    [string] $Lane,

    [string] $Directory,

    [ValidatePattern('^[0-9a-f]{40}$', Options = 'CultureInvariant')]
    [string] $Commit,

    [ValidatePattern('^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){2}(\.[1-9][0-9]*)?(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'PackageVerification.psm1')

$packagePrefix = 'TedToolkit.CppBindings'
$manifestName = 'release-manifest.json'
$platforms = [ordered]@{
    'win-x64' = 'Windows'
    'linux-x64' = 'Linux'
    'osx-arm64' = 'MacOS'
}
$providers = @('Occt', 'Cgal', 'Fcl', 'Manifold', 'Smlib')

function Get-ReleaseProjects([string] $RuntimeId) {
    if ($RuntimeId -ceq 'win-x64') {
        "src/shared/$packagePrefix.Runtime/$packagePrefix.Runtime.csproj"
        "src/shared/$packagePrefix.Generator/$packagePrefix.Generator.csproj"
        "src/tools/$packagePrefix.Analyzers/$packagePrefix.Analyzers.csproj"
        foreach ($provider in $providers) {
            $folder = $provider.ToLowerInvariant()
            $id = "$packagePrefix.$provider.Generator"
            "src/providers/$folder/$id/$id.csproj"
            if ($provider -cne 'Smlib') {
                $id = "$packagePrefix.$provider.Runtime"
                "src/providers/$folder/$id/$id.csproj"
            }
        }
    }

    foreach ($provider in $providers) {
        $folder = $provider.ToLowerInvariant()
        $id = "$packagePrefix.$provider.$($platforms[$RuntimeId])"
        "src/providers/$folder/$id/$id.csproj"
    }
}

function Assert-ReleaseEntries {
    param(
        [string] $Path,
        [string[]] $ExpectedNames,
        [switch] $Directories
    )

    $entries = @(Get-ChildItem -LiteralPath $Path -Force)
    foreach ($entry in $entries) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $entry.PSIsContainer -ne $Directories.IsPresent -or
            $entry.Name -cnotin $ExpectedNames) {
            throw "Unexpected release entry '$($entry.FullName)'."
        }
    }

    $names = @($entries | ForEach-Object { $_.Name })
    foreach ($name in $ExpectedNames) {
        if ($name -cnotin $names) {
            throw "Missing release entry '$name' in '$Path'."
        }
    }
}

function Assert-ProviderPackageRuntime([string] $Path, [string] $RuntimeId) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $runtimeEntries = @($archive.Entries | Where-Object {
            $_.Name -ne '' -and $_.FullName.StartsWith('runtimes/', [StringComparison]::OrdinalIgnoreCase)
        })
        $nativeRoot = "runtimes/$RuntimeId/native/"
        if (-not ($runtimeEntries | Where-Object {
                $_.FullName.StartsWith($nativeRoot, [StringComparison]::Ordinal)
            })) {
            throw "Package '$Path' has no native assets for '$RuntimeId'."
        }
        foreach ($entry in $runtimeEntries) {
            if (-not $entry.FullName.StartsWith("runtimes/$RuntimeId/", [StringComparison]::Ordinal)) {
                throw "Package '$Path' includes an asset for an unexpected runtime: '$($entry.FullName)'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-ReleaseLane {
    param(
        [string] $Path,
        [string] $RuntimeId,
        [switch] $ValidateManifest
    )

    $projects = @(Get-ReleaseProjects $RuntimeId)
    $packageIds = @($projects | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    $expectedNames = @($packageIds | ForEach-Object { "$_.$Version.nupkg" })
    $manifestPath = Join-Path $Path $manifestName
    if ($ValidateManifest -or (Test-Path -LiteralPath $manifestPath)) {
        $expectedNames += $manifestName
    }
    Assert-ReleaseEntries -Path $Path -ExpectedNames $expectedNames

    $packages = foreach ($id in $packageIds) {
        $artifact = Get-NuGetPackageArtifact -Feed $Path -PackageId $id
        if ($artifact.Version -cne $Version) {
            throw "Package '$id' has version '$($artifact.Version)', expected '$Version'."
        }
        if ($id.EndsWith(".$($platforms[$RuntimeId])", [StringComparison]::Ordinal)) {
            Assert-ProviderPackageRuntime $artifact.Path $RuntimeId
        }
        [pscustomobject] [ordered] @{
            id = $id
            file = [IO.Path]::GetFileName($artifact.Path)
            sha256 = (Get-FileHash -LiteralPath $artifact.Path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $manifest = [ordered] @{
        schema = 1
        lane = $RuntimeId
        commit = $Commit
        version = $Version
        packages = @($packages)
    } | ConvertTo-Json -Depth 4 -Compress

    if ($ValidateManifest) {
        $recorded = Get-Content -LiteralPath $manifestPath -Raw
        if ($recorded -cne $manifest) {
            throw "Release manifest for '$RuntimeId' does not match commit '$Commit', version '$Version', or the current package hashes."
        }
        foreach ($package in $packages) {
            Join-Path $Path $package.file
        }
    }
    else {
        Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding utf8NoBOM -NoNewline
    }
}

if ($Mode -in @('Projects', 'Record') -and -not $Lane) {
    throw "Specify -Lane for '$Mode'."
}
if ($Mode -eq 'Projects') {
    Get-ReleaseProjects $Lane
    return
}
if (-not $Directory -or -not $Commit -or -not $Version) {
    throw "Specify -Directory, -Commit, and -Version for '$Mode'."
}
if ($Mode -eq 'Record') {
    Get-ReleaseLane -Path $Directory -RuntimeId $Lane
    return
}
if ($Lane) {
    throw "The '$Mode' mode requires the full release directory."
}

Assert-ReleaseEntries -Path $Directory -ExpectedNames @($platforms.Keys) -Directories
$paths = @(
    foreach ($runtimeId in $platforms.Keys) {
        Get-ReleaseLane -Path (Join-Path $Directory $runtimeId) -RuntimeId $runtimeId -ValidateManifest
    }
)
Write-Output "Verified $($paths.Count) NuGet packages for $Commit at version $Version."

if ($Mode -eq 'Publish') {
    if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_REF -cne 'refs/heads/main' -or $env:GITHUB_SHA -cne $Commit) {
        throw 'NuGet publication requires a GitHub Actions run for the verified main commit.'
    }
    if (-not $env:NUGET_SOURCE -or -not $env:NUGET_API_KEY) {
        throw 'NuGet publication requires NUGET_SOURCE and NUGET_API_KEY.'
    }
    foreach ($path in $paths) {
        & dotnet nuget push $path --source $env:NUGET_SOURCE --api-key $env:NUGET_API_KEY --skip-duplicate
        if ($LASTEXITCODE -ne 0) {
            throw "Publishing '$path' failed with exit code $LASTEXITCODE."
        }
    }
}
