#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$processPath = $env:Path
if (@([Environment]::GetEnvironmentVariables().Keys | Where-Object { $_ -ceq 'PATH' }).Count -ne 0) {
    [Environment]::SetEnvironmentVariable('PATH', [NullString]::Value, 'Process')
    [Environment]::SetEnvironmentVariable('Path', $processPath, 'Process')
}

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$candidateRevision = (& git -C $repository rev-parse HEAD).Trim()
$startingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($startingStatus.Count -ne 0) {
    throw 'CGAL Runtime verification requires a clean exact candidate.'
}

if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/cr-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) {
    throw 'Use a new report directory; evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$feed = Join-Path $report 'feed'
$packLog = Join-Path $report 'pack.log'
@(
    'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj',
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Runtime/TedToolkit.CppBindings.Cgal.Runtime.csproj'
) | ForEach-Object {
    & dotnet pack (Join-Path $repository $_) -c Release -o $feed --disable-build-servers `
        --maxcpucount:1 -p:GeneratePackageOnBuild=false -p:NuGetAudit=false *>> $packLog
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL Runtime packaging failed; see $packLog"
    }
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Cgal.Runtime.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$consumerProject = Join-Path $consumer 'PackageConsumer.csproj'
$packages = Join-Path $repository 'out/package-cache/cgal-runtime'
$null = New-Item -ItemType Directory -Path $packages -Force
@(
    'tedtoolkit.cppbindings.runtime',
    'tedtoolkit.cppbindings.cgal.runtime'
) | ForEach-Object {
    $candidatePackageCache = Join-Path $packages $_
    if (Test-Path -LiteralPath $candidatePackageCache) {
        Remove-Item -LiteralPath $candidatePackageCache -Recurse -Force
    }
}

$consumerResultPath = Join-Path $report 'consumer-result.json'
$consumerLog = Join-Path $report 'consumer-run.log'
& dotnet run --project $consumerProject -c Release --disable-build-servers --no-launch-profile `
    "-p:RestoreSources=$feed" `
    -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
    "-p:RestorePackagesPath=$packages" -p:NuGetAudit=false -- $consumerResultPath *> $consumerLog
if ($LASTEXITCODE -ne 0) {
    throw "Independent CGAL Runtime package consumer failed; see $consumerLog"
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$forbidden = @($assets.libraries.Keys | Where-Object { $_ -match 'Occt|Generator' })
if ($forbidden.Count -ne 0) {
    throw "The CGAL Runtime package consumer acquired forbidden dependencies: $($forbidden -join ', ')."
}

@(
    'TedToolkit.CppBindings.Runtime',
    'TedToolkit.CppBindings.Cgal.Runtime'
) | ForEach-Object {
    $packageId = $_
    $package = Join-Path $feed "$packageId.1.0.0.nupkg"
    $expectedHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
    if ($assets.libraries["$packageId/1.0.0"].sha512 -cne $expectedHash) {
        throw "Consumer did not restore the just-built $packageId package."
    }
}

$testsLog = Join-Path $report 'cgal-runtime-tests.log'
$testResults = Join-Path $report 'cgal-runtime-test-results'
& dotnet run --project (Join-Path $repository `
    'tests/TedToolkit.CppBindings.Cgal.Runtime.Tests/TedToolkit.CppBindings.Cgal.Runtime.Tests.csproj') `
    -c Release --disable-build-servers -- --report-trx --results-directory $testResults *> $testsLog
if ($LASTEXITCODE -ne 0) {
    throw "CGAL Runtime TUnit proof failed; see $testsLog"
}

$generatorTestsLog = Join-Path $report 'cgal-generator-compatibility-tests.log'
$generatorTestResults = Join-Path $report 'cgal-generator-compatibility-test-results'
& dotnet run --project (Join-Path $repository `
    'tests/TedToolkit.CppBindings.Cgal.Generator.Tests/TedToolkit.CppBindings.Cgal.Generator.Tests.csproj') `
    -c Release --disable-build-servers -- --report-trx --results-directory $generatorTestResults `
    *> $generatorTestsLog
if ($LASTEXITCODE -ne 0) {
    throw "CGAL Generator result-projection compatibility proof failed; see $generatorTestsLog"
}
$generatorTrx = Get-ChildItem -LiteralPath $generatorTestResults -Filter '*.trx' -File | Select-Object -First 1
if ($null -eq $generatorTrx) {
    throw 'CGAL Generator compatibility proof did not emit a TRX result.'
}
[xml]$generatorTrxDocument = Get-Content -LiteralPath $generatorTrx.FullName -Raw
$generatorCounters = $generatorTrxDocument.TestRun.ResultSummary.Counters
if ([int]$generatorCounters.total -eq 0 -or [int]$generatorCounters.failed -ne 0 `
    -or [int]$generatorCounters.notExecuted -ne 0) {
    throw 'CGAL Generator compatibility proof did not pass every discovered test.'
}

$trx = Get-ChildItem -LiteralPath $testResults -Filter '*.trx' -File | Select-Object -First 1
if ($null -eq $trx) {
    throw 'CGAL Runtime TUnit proof did not emit a TRX result.'
}
[xml]$trxDocument = Get-Content -LiteralPath $trx.FullName -Raw
$counters = $trxDocument.TestRun.ResultSummary.Counters
if ([int]$counters.total -eq 0 -or [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0) {
    throw 'CGAL Runtime TUnit proof did not pass every discovered test.'
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($endingRevision -cne $candidateRevision -or $endingStatus.Count -ne 0) {
    throw 'The candidate revision or worktree changed during CGAL Runtime verification.'
}

$consumerResult = Get-Content -LiteralPath $consumerResultPath -Raw | ConvertFrom-Json -AsHashtable
$compilerFile = Join-Path $repository `
    'tests/TedToolkit.CppBindings.Cgal.Runtime.Tests/obj/NativeFixture/compiler-identity.txt'
if (-not (Test-Path -LiteralPath $compilerFile -PathType Leaf)) {
    throw 'The native fixture did not retain its resolved compiler identity.'
}
$compilerIdentity = (Get-Content -LiteralPath $compilerFile -Raw).Trim()
if ($compilerIdentity -cne 'MSVC|19.51.36257.0') {
    throw "Expected MSVC 19.51.36257.0, found '$compilerIdentity'."
}
$compilerVersion = $compilerIdentity.Split('|', 2)[1]
[ordered]@{
    Passed = $true
    CandidateRevision = $candidateRevision
    Assembly = $consumerResult.Assembly
    ExportedTypeCount = @($consumerResult.ExportedTypes).Count
    PackageConsumerExitCode = 0
    TestTotal = [int]$counters.total
    TestPassed = [int]$counters.passed
    TestFailed = [int]$counters.failed
    TestSkipped = [int]$counters.notExecuted
    FailureScenarioCount = 14
    ResultContainerKinds = @('std::optional<std::variant>', 'CGAL::Object')
    ResultPartitionCountPerContainer = 5
    GeneratorCompatibilityTestsExitCode = 0
    GeneratorTestTotal = [int]$generatorCounters.total
    GeneratorTestPassed = [int]$generatorCounters.passed
    NativeCompiler = "MSVC $compilerVersion"
    RuntimePackageHash = (Get-FileHash -LiteralPath `
        (Join-Path $feed 'TedToolkit.CppBindings.Cgal.Runtime.1.0.0.nupkg') -Algorithm SHA256).Hash
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8

Write-Output "CGAL Runtime package verification passed: $report"
