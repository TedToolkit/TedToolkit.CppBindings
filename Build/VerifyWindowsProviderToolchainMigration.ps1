#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-EqualValue {
    param([object] $Expected, [object] $Actual, [string] $Name)

    if ([string] $Expected -cne [string] $Actual) {
        throw "Toolchain migration changed ${Name}: expected '${Expected}', found '${Actual}'."
    }
}

function Assert-EqualMap {
    param([Collections.IDictionary] $Expected, [Collections.IDictionary] $Actual, [string] $Name)

    if ($Expected.Count -ne $Actual.Count) {
        throw "Toolchain migration changed ${Name} entry count."
    }

    foreach ($key in $Expected.Keys) {
        if (-not $Actual.Contains($key) -or [string] $Expected[$key] -cne [string] $Actual[$key]) {
            throw "Toolchain migration changed ${Name} entry '${key}'."
        }
    }
}

function Get-NormalizedHashes {
    param([string] $Root, [Collections.IDictionary] $Normalizations)

    $result = [ordered]@{}
    Get-ChildItem -LiteralPath $Root -File | Sort-Object -Property Name | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Raw
        foreach ($key in $Normalizations.Keys) {
            $text = $text.Replace([string] $key, [string] $Normalizations[$key])
        }

        $hash = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
        $result[$_.Name] = $hash.ToLowerInvariant()
    }

    return $result
}

function Get-DirectoryFingerprint {
    param([string] $Root)

    $entries = @(Get-ChildItem -LiteralPath $Root -File | Sort-Object -Property Name | ForEach-Object {
        $_.Name + '|' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    })
    $content = [Text.Encoding]::UTF8.GetBytes(($entries -join "`n"))
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($content)).ToLowerInvariant()
}

function Invoke-ProviderGeneration {
    param([string] $Name, [string] $Provider)

    $output = Join-Path $report $Name.ToLowerInvariant()
    $log = Join-Path $report ($Name.ToLowerInvariant() + '-generation.log')
    $project = Join-Path $repository 'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/TedToolkit.CppBindings.Windows.Generation.Tool.csproj'
    & dotnet run --project $project -c Release --disable-build-servers -- `
        --provider $Provider `
        --repository-root $repository `
        --output-root $output `
        --vcpkg-root $vcpkg `
        --configuration Release *> $log
    if ($LASTEXITCODE -ne 0) {
        throw "${Name} generation failed; see ${log}."
    }

    return $output
}

function Assert-Provider {
    param(
        [string] $Name,
        [Collections.IDictionary] $Expected,
        [Collections.IDictionary] $Migration,
        [string] $ProfileId,
        [string] $DeclaredMsvc,
        [string] $ResolvedMsvc,
        [Collections.IDictionary] $DependencyVersions,
        [string] $Output,
        [Collections.IDictionary] $Normalizations,
        [int] $NativeExportCount)

    Assert-EqualValue $Migration.To $ProfileId "${Name} profile"
    Assert-EqualValue $baseline.ExpectedMigration.Msvc.To $DeclaredMsvc "${Name} declared MSVC"
    Assert-EqualValue $baseline.ExpectedMigration.Msvc.To $ResolvedMsvc "${Name} resolved MSVC"
    Assert-EqualMap $Expected.DependencyVersions $DependencyVersions "${Name} dependency versions"

    $managedHashes = Get-NormalizedHashes (Join-Path $Output 'csharp') $Normalizations
    $nativeHashes = Get-NormalizedHashes (Join-Path $Output 'cpp') $Normalizations
    Assert-EqualValue $Expected.ManagedArtifactCount $managedHashes.Count "${Name} managed artifact count"
    Assert-EqualValue $Expected.NativeArtifactCount $nativeHashes.Count "${Name} native artifact count"
    Assert-EqualMap $Expected.NormalizedManagedHashes $managedHashes "${Name} managed source hashes"
    Assert-EqualMap $Expected.NormalizedNativeHashes $nativeHashes "${Name} native source hashes"
    Assert-EqualValue @($Expected.NativeExports).Count $NativeExportCount "${Name} native export count"

    return [ordered]@{
        ProfileId = $ProfileId
        DeclaredMsvc = $DeclaredMsvc
        ResolvedMsvc = $ResolvedMsvc
        ManagedArtifactCount = $managedHashes.Count
        NativeArtifactCount = $nativeHashes.Count
        NativeExportCount = $NativeExportCount
    }
}

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$candidateRevision = (& git -C $repository rev-parse HEAD).Trim()
$startingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
$vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository (
        'out/verification/windows-toolchain-migration-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) {
    throw 'Use a new report directory; migration evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$baselinePath = Join-Path $PSScriptRoot 'Baselines/windows-provider-toolchain-v1.json'
$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json -AsHashtable
$originalVcpkgRoot = $env:VCPKG_ROOT
try {
    $env:VCPKG_ROOT = $vcpkg

    $cgalOutput = Join-Path $report 'cgal'
    $null = Invoke-ProviderGeneration -Name 'Cgal' -Provider 'cgal'
    $cgalResult = Get-Content -LiteralPath (Join-Path $cgalOutput 'generation-result.json') -Raw | ConvertFrom-Json -AsHashtable
    $cgalManifest = Get-Content -LiteralPath (Join-Path $cgalOutput 'csharp/profile-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $cgalProfile = $cgalManifest.Profile
    $cgalDependencies = [ordered]@{
        Cgal = $cgalResult.Toolchain.Cgal
        Gmp = $cgalResult.Toolchain.Gmp
        Mpfr = $cgalResult.Toolchain.Mpfr
        CMake = $cgalResult.Toolchain.CMake
        Triplet = $cgalProfile.Triplet
        VcpkgBuiltinBaseline = $cgalProfile.VcpkgBuiltinBaseline
    }
    $cgalNormalizations = [ordered]@{
        ([string] $cgalProfile.ProfileId) = '${PROFILE_ID}'
        ([string] $cgalProfile.Toolchain.Msvc) = '${MSVC}'
        ([string] $cgalResult.Toolchain.CgalAbi) = '${CGAL_ABI}'
        ([string] $cgalResult.Toolchain.GmpAbi) = '${GMP_ABI}'
        ([string] $cgalResult.Toolchain.MpfrAbi) = '${MPFR_ABI}'
    }
    $cgalSummary = Assert-Provider -Name 'Cgal' -Expected $baseline.Providers.Cgal -Migration $baseline.ExpectedMigration.Profiles.Cgal -ProfileId $cgalProfile.ProfileId -DeclaredMsvc $cgalProfile.Toolchain.Msvc -ResolvedMsvc $cgalResult.Toolchain.Msvc -DependencyVersions $cgalDependencies -Output $cgalOutput -Normalizations $cgalNormalizations -NativeExportCount $cgalResult.NativeExportCount

    $fclOutput = Join-Path $report 'fcl'
    $null = Invoke-ProviderGeneration -Name 'Fcl' -Provider 'fcl'
    $fclResult = Get-Content -LiteralPath (Join-Path $fclOutput 'generation-result.json') -Raw | ConvertFrom-Json -AsHashtable
    $fclProfile = Get-Content -LiteralPath (Join-Path $fclOutput 'csharp/profile-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $fclDependencies = [ordered]@{}
    foreach ($key in $fclProfile.Versions.Keys) {
        if ($key -cne 'Msvc') {
            $fclDependencies[$key] = $fclProfile.Versions[$key]
        }
    }

    $fclNormalizations = [ordered]@{
        ([string] $fclProfile.ProfileId) = '${PROFILE_ID}'
        ([string] $fclProfile.Versions.Msvc) = '${MSVC}'
    }
    $fclSummary = Assert-Provider -Name 'Fcl' -Expected $baseline.Providers.Fcl -Migration $baseline.ExpectedMigration.Profiles.Fcl -ProfileId $fclProfile.ProfileId -DeclaredMsvc $fclProfile.Versions.Msvc -ResolvedMsvc $fclProfile.Versions.Msvc -DependencyVersions $fclDependencies -Output $fclOutput -Normalizations $fclNormalizations -NativeExportCount $fclResult.NativeFunctionCount

    $manifoldOutput = Join-Path $report 'manifold'
    $null = Invoke-ProviderGeneration -Name 'Manifold' -Provider 'manifold'
    $manifoldResult = Get-Content -LiteralPath (Join-Path $manifoldOutput 'generation-result.json') -Raw | ConvertFrom-Json -AsHashtable
    $manifoldProfile = Get-Content -LiteralPath (Join-Path $manifoldOutput 'csharp/profile-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $manifoldDependencies = [ordered]@{
        Manifold = $manifoldProfile.ManifoldVersion
        CMake = $manifoldProfile.CMake
        Triplet = $manifoldProfile.Triplet
        VcpkgBuiltinBaseline = $manifoldProfile.VcpkgBuiltinBaseline
    }
    $manifoldNormalizations = [ordered]@{
        ([string] $manifoldProfile.ProfileId) = '${PROFILE_ID}'
        ([string] $manifoldProfile.Msvc) = '${MSVC}'
    }
    $manifoldSummary = Assert-Provider -Name 'Manifold' -Expected $baseline.Providers.Manifold -Migration $baseline.ExpectedMigration.Profiles.Manifold -ProfileId $manifoldProfile.ProfileId -DeclaredMsvc $manifoldProfile.Msvc -ResolvedMsvc $manifoldProfile.Msvc -DependencyVersions $manifoldDependencies -Output $manifoldOutput -Normalizations $manifoldNormalizations -NativeExportCount $manifoldResult.NativeExportCount

    $occtOutput = Join-Path $report 'occt'
    $null = Invoke-ProviderGeneration -Name 'Occt' -Provider 'occt'
    $occtManagedRoot = Join-Path $occtOutput 'csharp'
    $occtNativeRoot = Join-Path $occtOutput 'cpp'
    $occtManagedCount = @(Get-ChildItem -LiteralPath $occtManagedRoot -File).Count
    $occtNativeCount = @(Get-ChildItem -LiteralPath $occtNativeRoot -File).Count
    $occtManagedFingerprint = Get-DirectoryFingerprint $occtManagedRoot
    $occtNativeFingerprint = Get-DirectoryFingerprint $occtNativeRoot
    Assert-EqualValue $baseline.Providers.Occt.ManagedArtifactCount $occtManagedCount 'Occt managed artifact count'
    Assert-EqualValue $baseline.Providers.Occt.NativeArtifactCount $occtNativeCount 'Occt native artifact count'
    Assert-EqualValue $baseline.Providers.Occt.ManagedFingerprint $occtManagedFingerprint 'Occt managed source fingerprint'
    Assert-EqualValue $baseline.Providers.Occt.NativeFingerprint $occtNativeFingerprint 'Occt native source fingerprint'
    $occtSummary = [ordered]@{
        ManagedArtifactCount = $occtManagedCount
        ManagedFingerprint = $occtManagedFingerprint
        NativeArtifactCount = $occtNativeCount
        NativeFingerprint = $occtNativeFingerprint
    }
}
finally {
    $env:VCPKG_ROOT = $originalVcpkgRoot
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($candidateRevision -cne $endingRevision -or ($startingStatus -join [Environment]::NewLine) -cne ($endingStatus -join [Environment]::NewLine)) {
    throw 'The source candidate changed during toolchain migration verification.'
}

[ordered]@{
    CandidateRevision = $candidateRevision
    BaselineSourceRevision = $baseline.SourceRevision
    Cgal = $cgalSummary
    Fcl = $fclSummary
    Manifold = $manifoldSummary
    Occt = $occtSummary
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $report 'verification-summary.json') -Encoding utf8

Write-Output "Windows provider toolchain migration verification passed. Evidence: $report"
