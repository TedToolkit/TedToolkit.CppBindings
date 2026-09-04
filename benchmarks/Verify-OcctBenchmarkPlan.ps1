#Requires -Version 7.5

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$baselineRepository = (Resolve-Path (Join-Path $repository '../wic-base')).Path
$proofRoot = Join-Path $repository "out/benchmark/occt-plan-proof-$([Guid]::NewGuid().ToString('N'))"
$builder = Join-Path $PSScriptRoot 'New-OcctBenchmarkPlan.ps1'
$matrixRunner = Join-Path $PSScriptRoot 'Invoke-BenchmarkMatrix.ps1'
$adapter = Join-Path $PSScriptRoot 'Invoke-OcctBenchmarkWorkload.ps1'

function Write-TextFile {
    param([string] $Path, [string] $Value)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Require {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

try {
    $baselineInput = Join-Path $proofRoot 'in-base'
    $candidateInput = Join-Path $proofRoot 'in-cand'
    $toolchain = Join-Path $proofRoot 'toolchain'
    $baselineInclude = Join-Path $baselineInput 'installed/x64-windows/include/opencascade'
    $candidateInclude = Join-Path $candidateInput 'installed/x64-windows/include/opencascade'
    foreach ($path in @($baselineInclude, $candidateInclude)) {
        Write-TextFile (Join-Path $path 'Fixture.hxx') "enum class Fixture { Original };`n"
        Write-TextFile (Join-Path $path 'Stable.hxx') "struct Stable {};`n"
    }
    foreach ($path in @($baselineInput, $candidateInput)) {
        Write-TextFile (Join-Path $path 'installed/vcpkg/status') "Package: occt`nVersion: fixture`n"
    }
    Write-TextFile (Join-Path $toolchain 'scripts/buildsystems/vcpkg.cmake') '# fixture toolchain'
    $changedHeader = Join-Path $proofRoot 'Fixture.changed.hxx'
    Write-TextFile $changedHeader "enum class Fixture { Original, AddedForBenchmark };`n"
    $generatorPatch = Join-Path $proofRoot 'generator-change.patch'
    Write-TextFile $generatorPatch "diff --git a/fixture b/fixture`n+// benchmark-only comment`n"

    $baselineHosts = Join-Path $proofRoot 'hosts-base'
    $candidateHosts = Join-Path $proofRoot 'hosts-cand'
    $baselineOriginal = Join-Path $baselineHosts 'original/Console.dll'
    $baselineChanged = Join-Path $baselineHosts 'changed/Console.dll'
    $candidateOriginal = Join-Path $candidateHosts 'original/Console.dll'
    $candidateChanged = Join-Path $candidateHosts 'changed/Console.dll'
    Write-TextFile $baselineOriginal 'baseline-original'
    Write-TextFile $baselineChanged 'baseline-changed'
    Write-TextFile $candidateOriginal 'candidate-original'
    Write-TextFile $candidateChanged 'candidate-changed'

    $tools = Join-Path $proofRoot 'tools'
    $dotnet = Join-Path $tools 'dotnet.exe'
    $cmake = Join-Path $tools 'cmake.exe'
    $ninja = Join-Path $tools 'ninja.exe'
    $compiler = Join-Path $tools 'cl.exe'
    $vcvars = Join-Path $tools 'vcvars64.bat'
    foreach ($path in @($dotnet, $cmake, $ninja, $compiler, $vcvars)) { Write-TextFile $path $path }

    $arguments = @{
        SpecificationDirectory = Join-Path $proofRoot 'specification'
        BaselineRepositoryRoot = $baselineRepository
        CandidateRepositoryRoot = $repository
        BaselineArtifactRoot = Join-Path $proofRoot 'artifact-b'
        CandidateArtifactRoot = Join-Path $proofRoot 'artifact-c'
        BaselineInputVcpkgRoot = $baselineInput
        CandidateInputVcpkgRoot = $candidateInput
        ToolchainVcpkgRoot = $toolchain
        BaselineOriginalHost = $baselineOriginal
        BaselineChangedHost = $baselineChanged
        CandidateOriginalHost = $candidateOriginal
        CandidateChangedHost = $candidateChanged
        GeneratorChangePatch = $generatorPatch
        DeclarationHeaderRelativePath = 'opencascade/Fixture.hxx'
        DeclarationChangedFile = $changedHeader
        MissingOutputRelativePath = 'cpp/Fixture.cpp'
        DotNetPath = $dotnet
        CMakePath = $cmake
        NinjaPath = $ninja
        CompilerPath = $compiler
        VcVarsPath = $vcvars
        DeadlineUtc = [DateTimeOffset]::UtcNow.AddHours(2)
        MemoryLimitBytes = 1073741824
        MemoryReserveBytes = 1073741824
    }
    & $builder @arguments
    if ($LASTEXITCODE -ne 0) { throw 'The fixture OCCT plan did not freeze.' }

    $specification = $arguments.SpecificationDirectory
    $plan = Get-Content -LiteralPath (Join-Path $specification 'occt-plan.json') -Raw | ConvertFrom-Json
    $matrix = Get-Content -LiteralPath (Join-Path $specification 'matrix.json') -Raw | ConvertFrom-Json
    Require ($plan.BaselineRevision -ceq 'e94f10a9bb9490d47363cf43d8ce17600b435b8a') 'Baseline binding drifted.'
    Require ($plan.CandidateBehaviorRevision -ceq '9952e5a76358028c22c8ec215a23d7b82413ad4f') 'Behavior binding drifted.'
    Require ($plan.HarnessBinding -ceq 'commit:PENDING-FINAL-HARNESS-COMMIT') 'Pending harness binding was lost.'
    Require ($plan.ProductionAdoptionAuthorized -eq $false) 'The experiment acquired production authority.'
    Require ($plan.Variants.baseline.ArtifactRoot.Length -eq $plan.Variants.candidate.ArtifactRoot.Length) 'Artifact roots are not equal length.'
    Require ($plan.Variants.baseline.InputVcpkgRoot.Length -eq $plan.Variants.candidate.InputVcpkgRoot.Length) 'Input roots are not equal length.'
    Require ($plan.ToolchainVcpkgRoot -cne $plan.Variants.baseline.InputVcpkgRoot) 'Toolchain and editable inputs overlap.'
    Require ((@($matrix.Workloads.Name) -join ',') -ceq 'artifact-cold,unchanged,declaration-edit,generator-change,missing-output') 'Workload order or inventory drifted.'

    foreach ($workload in $matrix.Workloads) {
        $expectedSamples = if ($workload.Name -eq 'artifact-cold') { 3 } else { 5 }
        Require ($workload.Samples -eq $expectedSamples) "Wrong sample count: $($workload.Name)"
        foreach ($variant in @('baseline', 'candidate')) {
            $measure = @($workload.$variant.Measure | ForEach-Object {
                Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json
            })
            $actions = @($measure | ForEach-Object { $_.Arguments[$_.Arguments.IndexOf('-Action') + 1] })
            if ($workload.Name -eq 'artifact-cold') {
                Require (($actions -join ',') -ceq 'Generate,Configure,Build') 'Artifact-cold stage plan is incomplete.'
            }
            else {
                Require (($actions -join ',') -ceq 'Generate,Build') "A warm workload attempted cmake --fresh: $($workload.Name)"
            }
            foreach ($stage in @($workload.$variant.Prepare + $workload.$variant.Measure + $workload.$variant.Verify)) {
                $text = Get-Content -LiteralPath $stage -Raw
                Require (-not $text.Contains('GenerateWindowsBindings.ps1', [StringComparison]::OrdinalIgnoreCase)) 'A stage uses the cached production wrapper.'
                Require ($text.Contains('{SampleRoot}', [StringComparison]::Ordinal)) 'A stage lost per-sample evidence routing.'
            }
            $expectedVerifyCount = if ($workload.Name -in @('declaration-edit', 'generator-change')) { 2 } else { 1 }
            Require (@($workload.$variant.Verify).Count -eq $expectedVerifyCount) "Wrong settle plan: $($workload.Name)"
        }
    }

    $planReport = Join-Path $proofRoot 'matrix-plan-only'
    & $matrixRunner -SpecificationPath (Join-Path $specification 'matrix.json') -ReportDirectory $planReport -PlanOnly
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $planReport 'plan.json'))) {
        throw 'The generated matrix did not pass plan-only validation.'
    }

    $sampleRoot = Join-Path $proofRoot 'pending-binding-sample'
    $null = New-Item -ItemType Directory -Path $sampleRoot
    $rejected = $false
    try {
        & $adapter -PlanPath (Join-Path $specification 'occt-plan.json') -Action Prepare `
            -Variant baseline -Workload artifact-cold -SampleRoot $sampleRoot
    }
    catch { $rejected = $_.Exception.Message -eq 'Formal workload execution is disabled until the final harness commit is bound.' }
    Require $rejected 'The pending final harness binding did not fail closed.'

    $unequal = $arguments.Clone()
    $unequal.SpecificationDirectory = Join-Path $proofRoot 'unequal-specification'
    $unequal.BaselineArtifactRoot = Join-Path $proofRoot 'short'
    $unequal.CandidateArtifactRoot = Join-Path $proofRoot 'materially-longer'
    $unequalRejected = $false
    try { & $builder @unequal }
    catch { $unequalRejected = $_.Exception.Message -eq 'Baseline and candidate artifact roots must have equal absolute path lengths.' }
    Require $unequalRejected 'Unequal artifact paths did not fail closed.'

    Write-Output "OCCT benchmark plan proof passed: exact revisions, private inputs, equal paths, direct hosts, five workloads, cold-only configure, settlement, bindings, and plan-only guards. Evidence: $proofRoot"
}
finally {
    if (Test-Path -LiteralPath $proofRoot) { Remove-Item -LiteralPath $proofRoot -Recurse -Force }
}
