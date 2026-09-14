$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptPath = Join-Path $PSScriptRoot 'Get-BenchmarkEnvironment.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref] $tokens, [ref] $parseErrors)
if ($parseErrors.Count -gt 0) { throw ($parseErrors.Message -join '; ') }

# Exercise the actual private predicate without probing or changing this machine.
$predicate = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Test-CompetingBuild'
}, $false)
if ($null -eq $predicate) { throw 'The competing-build predicate was not found.' }
. ([scriptblock]::Create($predicate.Extent.Text))
$cases = @(
    @{ Name = 'cl.exe'; CommandLine = 'cl source.cpp'; Expected = $true },
    @{ Name = 'ninja.exe'; CommandLine = 'ninja'; Expected = $true },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet build project.csproj'; Expected = $true },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet run --project host'; Expected = $true },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet MSBuild.dll temporary.proj'; Expected = $true },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet MSBuild.dll /nodemode:1 /nodeReuse:true'; Expected = $false },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet Roslyn.Worker.exe'; Expected = $false },
    @{ Name = 'dotnet.exe'; CommandLine = 'dotnet OmniSharp.dll -s project.sln'; Expected = $false },
    @{ Name = 'explorer.exe'; CommandLine = $null; Expected = $false }
)
foreach ($case in $cases) {
    if ((Test-CompetingBuild ([pscustomobject] $case)) -ne $case.Expected) {
        throw "Unexpected competing-build classification: $($case.CommandLine)"
    }
}

$before = (Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash
$rejected = $false
try {
    . (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')
    & $scriptPath -RepositoryRoot $PSScriptRoot -ReportPath $scriptPath -ArtifactProbePath $PSScriptRoot `
        -ExpectedArtifactVolumeIdentity (Get-BenchmarkVolumeIdentity $PSScriptRoot)
}
catch { $rejected = $_.Exception.Message -like 'Refusing to overwrite an existing report:*' }
if (-not $rejected) { throw 'The existing-report guard did not reject the destination.' }
if ((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -ne $before) {
    throw 'The existing-report guard changed the destination.'
}
Write-Output "Benchmark preflight proof passed: $($cases.Count) classifications, syntax, and non-overwrite guard."
