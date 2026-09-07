#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/analyzer-package-' + [Guid]::NewGuid().ToString('N'))
}
$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) { throw 'Use a new report directory; verification evidence is not overwritten.' }
$null = New-Item -ItemType Directory -Path $report
$feed = Join-Path $report 'feed'
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Analyzers.Tests/Fixtures/PackageConsumer'
$projects = @{
    'TedToolkit.CppBindings.Runtime' = 'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Occt.Runtime' = 'src/providers/occt/TedToolkit.CppBindings.Occt.Runtime/TedToolkit.CppBindings.Occt.Runtime.csproj'
    'TedToolkit.CppBindings.Analyzers' = 'src/tools/TedToolkit.CppBindings.Analyzers/TedToolkit.CppBindings.Analyzers.csproj'
}
foreach ($name in $projects.Keys | Sort-Object) {
    $log = Join-Path $report ($name + '-pack.log')
    & dotnet pack (Join-Path $repository $projects[$name]) -c Release -o $feed --disable-build-servers --maxcpucount:1 -p:GeneratePackageOnBuild=false -p:NuGetAudit=false *> $log
    if ($LASTEXITCODE -ne 0) { throw "Package creation failed; see $log" }
    $package = Join-Path $feed ($name + '.1.0.0.nupkg')
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entries = @($archive.Entries.FullName)
        if ($name -eq 'TedToolkit.CppBindings.Analyzers') {
            $expected = 'analyzers/dotnet/cs/TedToolkit.CppBindings.Analyzers.dll'
            if (@($entries | Where-Object { $_ -like '*.dll' }).Count -ne 1 -or $entries -notcontains $expected) {
                throw 'The analyzer package must contain only the compiler asset, without a runtime library.'
            }
            $reader = [IO.StreamReader]::new($archive.GetEntry($name + '.nuspec').Open())
            try { [xml] $nuspec = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
            if ($nuspec.SelectNodes('//*[local-name()="dependency"]').Count -ne 0) {
                throw 'The analyzer package unexpectedly introduces package/runtime dependencies.'
            }
        }
        elseif (@($entries | Where-Object { $_ -like 'analyzers/*' }).Count -ne 0) {
            throw 'Runtime must not embed analyzer assets.'
        }
    }
    finally { $archive.Dispose() }
}

$scenarios = @(
    @{ Symbol = 'ANALYZER_ONLY'; Runtime = 'false'; Occt = 'false'; Expected = @() },
    @{ Symbol = 'VALID'; Runtime = 'true'; Occt = 'false'; Expected = @() },
    @{ Symbol = 'GENERIC_HOOK'; Runtime = 'true'; Occt = 'false'; Expected = @('TTCB001') },
    @{ Symbol = 'GENERIC_LIFETIME'; Runtime = 'true'; Occt = 'false'; Expected = @('TTCB002') },
    @{ Symbol = 'CUSTOM_OWNER'; Runtime = 'true'; Occt = 'false'; Expected = @('TTCB002') },
    @{ Symbol = 'OCCT_HOOK'; Runtime = 'true'; Occt = 'true'; Expected = @('TTCB001') },
    @{ Symbol = 'OCCT_LIFETIME'; Runtime = 'true'; Occt = 'true'; Expected = @('TTCB002') },
    @{ Symbol = 'BORROWED'; Runtime = 'true'; Occt = 'true'; Expected = @() },
    @{ Symbol = 'SUPPRESSED'; Runtime = 'true'; Occt = 'false'; Expected = @() }
)
$results = [Collections.Generic.List[object]]::new()
foreach ($scenario in $scenarios) {
    $consumer = Join-Path $report $scenario.Symbol
    $null = New-Item -ItemType Directory -Path $consumer
    Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
    $diagnosticPath = Join-Path $consumer 'diagnostics.sarif'
    $log = Join-Path $consumer 'build.log'
    $arguments = @('build', (Join-Path $consumer 'PackageConsumer.csproj'), '-c', 'Release',
        '--disable-build-servers', '--maxcpucount:1', ('-p:RestoreSources=' + $feed),
        '-p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json',
        ('-p:RestorePackagesPath=' + (Join-Path $report 'packages')), ('-p:DefineConstants=' + $scenario.Symbol),
        ('-p:UseRuntime=' + $scenario.Runtime), ('-p:UseOcct=' + $scenario.Occt), ('-p:ErrorLog=' + $diagnosticPath))
    & dotnet @arguments *> $log
    $exitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $diagnosticPath)) { throw "Compiler evidence missing; see $log" }
    $sarif = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json -AsHashtable
    $errors = @($sarif.runs[0].results | Where-Object { $_.level -eq 'error' })
    $diagnostics = @($errors | Where-Object { -not $_.ContainsKey('suppressionStates') } | ForEach-Object { $_.ruleId } | Sort-Object)
    $suppressed = @($errors | Where-Object { $_.ContainsKey('suppressionStates') -and $_.suppressionStates -contains 'suppressedInSource' } | ForEach-Object { $_.ruleId } | Sort-Object)
    if ($scenario.Symbol -eq 'SUPPRESSED' -and ($suppressed -join ',') -cne 'TTCB001,TTCB002') {
        throw 'Suppression must be proven by both actual diagnostics recorded as suppressed in source.'
    }
    if (($diagnostics -join ',') -cne (($scenario.Expected | Sort-Object) -join ',') -or
        (($exitCode -eq 0) -ne ($scenario.Expected.Count -eq 0))) {
        throw "Unexpected compile result for $($scenario.Symbol): exit $exitCode, diagnostics $diagnostics; see $log"
    }
    $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($name in $projects.Keys) {
        $identity = $name + '/1.0.0'
        if ($assets.libraries.ContainsKey($identity)) {
            $package = Join-Path $feed ($name + '.1.0.0.nupkg')
            $expectedHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
            if ($assets.libraries[$identity].sha512 -cne $expectedHash) {
                throw "Consumer did not restore the just-built package: $identity"
            }
        }
    }
    foreach ($target in $assets.targets.Values) {
        $analyzer = $target['TedToolkit.CppBindings.Analyzers/1.0.0']
        if ($analyzer.ContainsKey('compile') -or $analyzer.ContainsKey('runtime')) {
            throw 'Analyzer assets became a compiler reference or runtime dependency.'
        }
    }
    if ($scenario.Runtime -eq 'false' -and @($assets.libraries.Keys | Where-Object { $_ -like '*Runtime/*' }).Count -ne 0) {
        throw 'Analyzer-only consumption unexpectedly pulled in Runtime.'
    }
    if ($scenario.Occt -eq 'false' -and @($assets.libraries.Keys | Where-Object { $_ -like '*Occt*' }).Count -ne 0) {
        throw 'A generic consumer unexpectedly pulled in the OCCT provider.'
    }
    $results.Add([pscustomobject]@{ Scenario = $scenario.Symbol; ExitCode = $exitCode; Diagnostics = $diagnostics; SuppressedDiagnostics = $suppressed; Arguments = $arguments })
    Write-Output "Packed consumer passed: $($scenario.Symbol)"
}
[ordered]@{
    Passed = $true
    Scenarios = @($results.ToArray())
    PackageHashes = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "Analyzer package verification passed: $report"
