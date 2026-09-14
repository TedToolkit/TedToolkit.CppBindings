param(
    [string] $RepositoryRoot,
    [string] $ReportPath,
    [string] $ArtifactProbePath,
    [string] $ExpectedArtifactVolumeIdentity
)
$ErrorActionPreference = 'Stop'
# Deterministic orchestration fixture only; never use this for machine readiness.
@{
    ResourcePreflightPassed = $RepositoryRoot -notlike '*resource-failure*'
    Memory = @{ FreeBytes = 16GB }
} | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding utf8
