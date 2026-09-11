#Requires -Version 7.5
param(
    [string] $RepositoryRoot,
    [string] $Configuration = 'Release',
    [string] $OutputPath,
    [string] $BaselinePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = if ($RepositoryRoot) {
    [IO.Path]::GetFullPath($RepositoryRoot)
}
else {
    [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
}
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repository 'output'))
$summaryPath = if ($OutputPath) {
    [IO.Path]::GetFullPath($OutputPath)
}
else {
    Join-Path $outputRoot 'windows-generation-summary.json'
}
if (-not $summaryPath.StartsWith(
        $outputRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must remain beneath '$outputRoot'."
}

$providers = @(
    [ordered]@{
        Name = 'Occt'
        Root = Join-Path $outputRoot 'generated'
        NativeLibrary = "native-build/$Configuration/ted_toolkit_occt.dll"
        Manifest = "native-build/$Configuration/managed-files.txt"
    },
    [ordered]@{
        Name = 'Cgal'
        Root = Join-Path $outputRoot 'providers/cgal'
        NativeLibrary = "native-build/$Configuration/ted_toolkit_cpp_bindings_cgal.dll"
        Manifest = 'output-manifest.json'
    },
    [ordered]@{
        Name = 'Manifold'
        Root = Join-Path $outputRoot 'providers/manifold'
        NativeLibrary = "native-build/$Configuration/ted_toolkit_cpp_bindings_manifold.dll"
        Manifest = 'output-manifest.json'
    },
    [ordered]@{
        Name = 'Fcl'
        Root = Join-Path $outputRoot 'providers/fcl'
        NativeLibrary = "native-build/$Configuration/ted_toolkit_cpp_bindings_fcl.dll"
        Manifest = 'output-manifest.json'
    }
)

$results = @($providers | ForEach-Object {
    $root = [IO.Path]::GetFullPath($_.Root)
    $managedRoot = Join-Path $root 'csharp'
    $managedFiles = @(Get-ChildItem -LiteralPath $managedRoot -Filter '*.cs' -File -ErrorAction SilentlyContinue |
        Sort-Object -Property Name)
    if ($managedFiles.Count -eq 0 -or @($managedFiles | Where-Object { $_.Length -eq 0 }).Count -ne 0) {
        throw "$($_.Name) did not produce a complete nonempty managed source set."
    }

    $nativeLibrary = Join-Path $root $_.NativeLibrary
    if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf) `
        -or (Get-Item -LiteralPath $nativeLibrary).Length -eq 0) {
        throw "$($_.Name) did not produce '$($_.NativeLibrary)'."
    }

    $manifest = Join-Path $root $_.Manifest
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf) `
        -or (Get-Item -LiteralPath $manifest).Length -eq 0) {
        throw "$($_.Name) did not produce '$($_.Manifest)'."
    }

    $sourceFingerprint = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes(($managedFiles | ForEach-Object {
                "$($_.Name)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
            }) -join "`n")))
    [ordered]@{
        Name = $_.Name
        ManagedSourceCount = $managedFiles.Count
        ManagedSourceFingerprint = $sourceFingerprint
        NativeLibrary = $_.NativeLibrary.Replace('\', '/')
        NativeLibraryHash = (Get-FileHash -LiteralPath $nativeLibrary -Algorithm SHA256).Hash
        Manifest = $_.Manifest.Replace('\', '/')
        ManifestHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
    }
})

if ($BaselinePath) {
    $resolvedBaseline = [IO.Path]::GetFullPath($BaselinePath)
    if (-not (Test-Path -LiteralPath $resolvedBaseline -PathType Leaf)) {
        throw "Generation baseline is missing: $resolvedBaseline"
    }

    $baseline = Get-Content -LiteralPath $resolvedBaseline -Raw | ConvertFrom-Json
    foreach ($result in $results) {
        $expected = @($baseline.providers | Where-Object {
                $_.name -eq $result.Name
            })
        if ($expected.Count -ne 1) {
            throw "Generation baseline must contain exactly one $($result.Name) entry."
        }

        if ($result.ManagedSourceCount -ne $expected[0].managedSourceCount `
            -or $result.ManagedSourceFingerprint -cne $expected[0].managedSourceFingerprint) {
            throw "$($result.Name) managed output differs from the approved baseline: " +
                "count=$($result.ManagedSourceCount)/$($expected[0].managedSourceCount), " +
                "fingerprint=$($result.ManagedSourceFingerprint)/$($expected[0].managedSourceFingerprint)."
        }
    }
}

$null = New-Item -ItemType Directory -Path (Split-Path $summaryPath -Parent) -Force
[ordered]@{
    Passed = $true
    Configuration = $Configuration
    Providers = $results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Output "Windows generation output verification passed: $summaryPath"
