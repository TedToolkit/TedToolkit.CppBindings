$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script = Join-Path $PSScriptRoot 'Get-ArtifactManifest.ps1'
$proofRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('out/benchmark/manifest-proof-' + [Guid]::NewGuid().ToString('N'))
$artifacts = Join-Path $proofRoot 'artifacts'
$null = New-Item -ItemType Directory -Path $artifacts
$first = Join-Path $artifacts 'a.cpp'
$second = Join-Path $artifacts 'b.cs'
$third = Join-Path $artifacts 'c.cpp'
$support = Join-Path $proofRoot 'support.txt'
[IO.File]::WriteAllText($first, 'native')
[IO.File]::WriteAllText($second, 'managed')
[IO.File]::WriteAllText($support, 'support')
$baseline = Join-Path $proofRoot 'baseline.json'
& $script -Roots @($artifacts) -Files @($support) -ReportPath $baseline
$initial = Get-Content -LiteralPath $baseline -Raw | ConvertFrom-Json
if ($initial.FileCount -ne 3 -or $initial.TotalBytes -ne 20 -or
    $initial.AdditionalFiles[0] -cne $support) { throw 'The artifact inventory is incorrect.' }

$rootOnly = Join-Path $proofRoot 'root-only.json'
& $script -Roots @($artifacts) -ReportPath $rootOnly
$legacy = Join-Path $proofRoot 'legacy-schema-1.json'
$legacyValue = Get-Content -LiteralPath $rootOnly -Raw | ConvertFrom-Json
$legacyValue.PSObject.Properties.Remove('AdditionalFiles')
$legacyValue | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $legacy -Encoding utf8
$legacyComparison = Join-Path $proofRoot 'legacy-comparison.json'
& $script -Roots @($artifacts) -ReportPath $legacyComparison -CompareTo $legacy
$legacyResult = Get-Content -LiteralPath $legacyComparison -Raw | ConvertFrom-Json
if (-not $legacyResult.Comparison.EqualContent) {
    throw 'Schema-1 manifests without AdditionalFiles must remain compatible with root-only comparisons.'
}
$rejected = $false
try { & $script -Roots @($artifacts) -ReportPath (Join-Path $proofRoot 'category-mismatch.json') -CompareTo $baseline }
catch { $rejected = $_.Exception.Message -eq 'The comparison manifest has an incompatible schema or artifact category count.' }
if (-not $rejected) { throw 'Artifact category-count mismatch was accepted.' }

$junctionTarget = Join-Path $proofRoot 'junction-target'
$junctionPath = Join-Path $proofRoot 'junction-alias'
$null = New-Item -ItemType Directory -Path $junctionTarget
$junctionFile = Join-Path $junctionTarget 'aliased.txt'
[IO.File]::WriteAllText($junctionFile, 'aliased')
$null = New-Item -ItemType Junction -Path $junctionPath -Target $junctionTarget
$rejected = $false
try {
    & $script -Roots @($artifacts) -Files @((Join-Path $junctionPath 'aliased.txt')) `
        -ReportPath (Join-Path $proofRoot 'junction-parent.json')
}
catch { $rejected = $_.Exception.Message -eq 'Artifact manifests do not follow symbolic links or junctions.' }
if (-not $rejected) { throw 'An explicit artifact file traversed a reparse-point parent.' }

$unchanged = Join-Path $proofRoot 'unchanged.json'
& $script -Roots @($artifacts) -Files @($support) -ReportPath $unchanged -CompareTo $baseline
$result = Get-Content -LiteralPath $unchanged -Raw | ConvertFrom-Json
if (-not $result.Comparison.EqualContent -or $result.Comparison.ObservedRewritten.Count -ne 0) {
    throw 'Unchanged artifacts must have equal content and zero observed rewrites.'
}
[IO.File]::SetLastWriteTimeUtc($first, [IO.File]::GetLastWriteTimeUtc($first).AddSeconds(2))
$touched = Join-Path $proofRoot 'touched.json'
& $script -Roots @($artifacts) -Files @($support) -ReportPath $touched -CompareTo $baseline
$result = Get-Content -LiteralPath $touched -Raw | ConvertFrom-Json
if (-not $result.Comparison.EqualContent -or $result.Comparison.ObservedRewritten[0] -ne '0/a.cpp') {
    throw 'A timestamp-only rewrite was not distinguished from a content change.'
}
[IO.File]::SetLastWriteTimeUtc($support, [IO.File]::GetLastWriteTimeUtc($support).AddSeconds(2))
$supportTouched = Join-Path $proofRoot 'support-touched.json'
& $script -Roots @($artifacts) -Files @($support) -ReportPath $supportTouched -CompareTo $baseline
$result = Get-Content -LiteralPath $supportTouched -Raw | ConvertFrom-Json
if (-not $result.Comparison.EqualContent -or
    $result.Comparison.ObservedRewritten -cnotcontains '1/support.txt') {
    throw 'A timestamp-only rewrite of an additional artifact file was not detected.'
}

[IO.File]::WriteAllText($first, 'changed native')
# Move the controlled fixture outside the artifact root; no user file is deleted.
Move-Item -LiteralPath $second -Destination (Join-Path $proofRoot 'removed-b.cs')
[IO.File]::WriteAllText($third, 'added')
$modified = Join-Path $proofRoot 'modified.json'
& $script -Roots @($artifacts) -Files @($support) -ReportPath $modified -CompareTo $baseline
$result = Get-Content -LiteralPath $modified -Raw | ConvertFrom-Json
if ($result.Comparison.EqualContent -or $result.Comparison.Added[0] -ne '0/c.cpp' -or
    $result.Comparison.Removed[0] -ne '0/b.cs' -or $result.Comparison.ContentChanged[0] -ne '0/a.cpp') {
    throw 'Added, removed, or content-changed artifacts were misclassified.'
}
$before = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash
$rejected = $false
try { & $script -Roots @($artifacts) -ReportPath $baseline }
catch { $rejected = $_.Exception.Message -eq 'Refusing to overwrite an artifact manifest.' }
if (-not $rejected -or (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash -ne $before) {
    throw 'The existing-manifest guard failed.'
}
$rejected = $false
try { & $script -Roots @($artifacts) -ReportPath (Join-Path $artifacts 'manifest.json') }
catch { $rejected = $_.Exception.Message -eq 'Store manifests outside the measured artifact roots.' }
if (-not $rejected) { throw 'The manifest would contaminate the measured corpus.' }
$rejected = $false
try { & $script -Roots @($artifacts) -Files @($first) -ReportPath (Join-Path $proofRoot 'duplicate-input.json') }
catch { $rejected = $_.Exception.Message -eq 'Additional artifact files must be outside the measured artifact roots.' }
if (-not $rejected) { throw 'An additional file duplicated content already covered by an artifact root.' }
Write-Output "Artifact manifest proof passed: directory and explicit-file inventory, legacy schema, category mismatch, junction-parent rejection, unchanged, timestamp-only, changed/added/removed, non-overwrite, duplicate-input rejection, and output isolation. Evidence: $proofRoot"
