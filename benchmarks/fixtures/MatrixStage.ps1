param([string] $SpecificationPath, [string] $ReportDirectory)
$ErrorActionPreference = 'Stop'
$spec = Get-Content -LiteralPath $SpecificationPath -Raw | ConvertFrom-Json -DateKind String
$null = New-Item -ItemType Directory -Path $ReportDirectory
$mode = $spec.Arguments[0]
$success = $mode -ne 'failure'
@{
    Succeeded = $success
    ProcessElapsedSeconds = 2.0
    DeadlineUtc = $spec.DeadlineUtc
    MemoryLimitBytes = $spec.MemoryLimitBytes
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReportDirectory 'result.json') -Encoding utf8
if ($mode -eq 'mutate') {
    Add-Content -LiteralPath $spec.Arguments[1] -Value 'changed by the controlled fixture'
}
if (-not $success) { throw 'Controlled stage failure.' }
