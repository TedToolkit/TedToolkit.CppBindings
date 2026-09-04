$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script = Join-Path $PSScriptRoot 'Get-ArtifactManifest.ps1'
$proofRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('out/benchmark/manifest-proof-' + [Guid]::NewGuid().ToString('N'))
$artifacts = Join-Path $proofRoot 'artifacts'
$null = New-Item -ItemType Directory -Path $artifacts
$first = Join-Path $artifacts 'a.cpp'
$second = Join-Path $artifacts 'b.cs'
$third = Join-Path $artifacts 'c.cpp'
[IO.File]::WriteAllText($first, 'native')
[IO.File]::WriteAllText($second, 'managed')
$baseline = Join-Path $proofRoot 'baseline.json'
& $script -Roots @($artifacts) -ReportPath $baseline
$initial = Get-Content -LiteralPath $baseline -Raw | ConvertFrom-Json
if ($initial.FileCount -ne 2 -or $initial.TotalBytes -ne 13) { throw 'The artifact inventory is incorrect.' }

$unchanged = Join-Path $proofRoot 'unchanged.json'
& $script -Roots @($artifacts) -ReportPath $unchanged -CompareTo $baseline
$result = Get-Content -LiteralPath $unchanged -Raw | ConvertFrom-Json
if (-not $result.Comparison.EqualContent -or $result.Comparison.ObservedRewritten.Count -ne 0) {
    throw 'Unchanged artifacts must have equal content and zero observed rewrites.'
}
[IO.File]::SetLastWriteTimeUtc($first, [IO.File]::GetLastWriteTimeUtc($first).AddSeconds(2))
$touched = Join-Path $proofRoot 'touched.json'
& $script -Roots @($artifacts) -ReportPath $touched -CompareTo $baseline
$result = Get-Content -LiteralPath $touched -Raw | ConvertFrom-Json
if (-not $result.Comparison.EqualContent -or $result.Comparison.ObservedRewritten[0] -ne '0/a.cpp') {
    throw 'A timestamp-only rewrite was not distinguished from a content change.'
}

[IO.File]::WriteAllText($first, 'changed native')
# Move the controlled fixture outside the artifact root; no user file is deleted.
Move-Item -LiteralPath $second -Destination (Join-Path $proofRoot 'removed-b.cs')
[IO.File]::WriteAllText($third, 'added')
$modified = Join-Path $proofRoot 'modified.json'
& $script -Roots @($artifacts) -ReportPath $modified -CompareTo $baseline
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
Write-Output "Artifact manifest proof passed: inventory, unchanged, timestamp-only, changed/added/removed, non-overwrite, and output isolation. Evidence: $proofRoot"
