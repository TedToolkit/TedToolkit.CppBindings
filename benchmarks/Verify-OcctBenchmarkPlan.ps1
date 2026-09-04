#Requires -Version 7.5

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$baselineRepository = (Resolve-Path (Join-Path $repository '../wic-base')).Path
$proofRoot = Join-Path ([IO.Path]::GetTempPath()) "occt-plan-proof-$([Guid]::NewGuid().ToString('N'))"
$builder = Join-Path $PSScriptRoot 'New-OcctBenchmarkPlan.ps1'
$receiptBuilder = Join-Path $PSScriptRoot 'New-OcctBenchmarkHostReceipt.ps1'
$matrixRunner = Join-Path $PSScriptRoot 'Invoke-BenchmarkMatrix.ps1'
$manifestTool = Join-Path $PSScriptRoot 'Get-ArtifactManifest.ps1'
$exportTool = Join-Path $PSScriptRoot 'Get-OcctExportInventory.ps1'
$adapter = Join-Path $PSScriptRoot 'Invoke-OcctBenchmarkWorkload.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)

function Write-TextFile {
    param([string] $Path, [string] $Value)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Value, $utf8)
}

function Write-JsonFile {
    param([string] $Path, $Value)
    Write-TextFile $Path ($Value | ConvertTo-Json -Depth 12)
}

function Require {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Git {
    param([string] $Root, [string[]] $Arguments)
    $output = @(& git -c "safe.directory=$Root" -C $Root @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "Fixture git command failed: $($Arguments -join ' ')" }
    return $output
}

function New-SyntheticBuildReceipt {
    param([string] $Directory, [string] $SourceRoot, [string] $DotNet, [string] $Project)
    $null = [IO.Directory]::CreateDirectory($Directory)
    $specPath = Join-Path $Directory 'build-specification.json'
    $resultPath = Join-Path $Directory 'build-result.json'
    $arguments = @('build', $Project, '-c', 'Release', '--no-restore')
    $specification = [ordered]@{ Executable = $DotNet; Arguments = $arguments; WorkingDirectory = $SourceRoot }
    Write-JsonFile $specPath $specification
    Write-JsonFile $resultPath ([ordered]@{
        SchemaVersion = 1; Succeeded = $true; ExitCode = 0
        SpecificationSha256 = (Get-FileHash $specPath).Hash
        Command = [ordered]@{ Executable = $DotNet; Arguments = $arguments; WorkingDirectory = $SourceRoot }
    })
    return [pscustomobject]@{ Specification = $specPath; Result = $resultPath }
}

function New-HostReceiptFixture {
    param(
        [string] $Name, [string] $Variant, [string] $State, [string] $SourceRoot,
        [string] $BaseRevision, [string] $SeedHost, [string] $DotNet, [string] $Project,
        [string] $PatchPath
    )
    $hostRoot = Join-Path $proofRoot "hosts/$Name"
    Copy-Item -LiteralPath $SeedHost -Destination $hostRoot -Recurse
    $build = New-SyntheticBuildReceipt (Join-Path $proofRoot "builds/$Name") $SourceRoot $DotNet $Project
    $receiptPath = Join-Path $proofRoot "receipts/$Name.json"
    $arguments = @{
        ReceiptPath = $receiptPath; Variant = $Variant; State = $State
        SourceRepositoryRoot = $SourceRoot; SourceBaseRevision = $BaseRevision
        HostDirectory = $hostRoot; HostEntryPointRelativePath = 'TedToolkit.CppBindings.Occt.Console.dll'
        BuildSpecificationPath = $build.Specification; BuildResultPath = $build.Result; FixtureOnly = $true
    }
    if ($PatchPath) { $arguments.FrozenPatchPath = $PatchPath }
    & $receiptBuilder @arguments | Out-Null
    return $receiptPath
}

try {
    $null = [IO.Directory]::CreateDirectory($proofRoot)
    $dotnet = (Get-Command dotnet -CommandType Application).Source
    $visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
    $toolchain = Get-ChildItem -LiteralPath $visualStudioRoot -Directory |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory } |
        Sort-Object FullName -Descending |
        ForEach-Object {
            $ninjaPath = Join-Path $_.FullName 'Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe'
            $cmakePath = Join-Path $_.FullName 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
            $vcvarsPath = Join-Path $_.FullName 'VC/Auxiliary/Build/vcvars64.bat'
            $compilerPath = Get-ChildItem -LiteralPath (Join-Path $_.FullName 'VC/Tools/MSVC') -Directory |
                Sort-Object Name -Descending |
                ForEach-Object { Join-Path $_.FullName 'bin/Hostx64/x64/cl.exe' } |
                Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
            if ($compilerPath -and (Test-Path $ninjaPath) -and (Test-Path $cmakePath) -and (Test-Path $vcvarsPath)) {
                [pscustomobject]@{ Ninja = $ninjaPath; CMake = $cmakePath; Compiler = $compilerPath; VcVars = $vcvarsPath }
            }
        } | Select-Object -First 1
    if ($null -eq $toolchain) { throw 'The lightweight plan verifier requires the repository Windows toolchain.' }

    $sourceOriginal = Join-Path $proofRoot 'source-original'
    $null = [IO.Directory]::CreateDirectory($sourceOriginal)
    Write-TextFile (Join-Path $sourceOriginal '.gitignore') "bin/`nobj/`n"
    Write-TextFile (Join-Path $sourceOriginal 'Fixture.csproj') @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>TedToolkit.CppBindings.Occt.Console</AssemblyName>
  </PropertyGroup>
</Project>
'@
    Write-TextFile (Join-Path $sourceOriginal 'Program.cs') "System.Console.WriteLine(`"fixture`");`n"
    $null = Invoke-Git $sourceOriginal @('init', '--quiet')
    $null = Invoke-Git $sourceOriginal @('config', 'user.name', 'OCCT benchmark fixture')
    $null = Invoke-Git $sourceOriginal @('config', 'user.email', 'fixture@example.invalid')
    $null = Invoke-Git $sourceOriginal @('add', '.')
    $null = Invoke-Git $sourceOriginal @('commit', '--quiet', '-m', 'fixture base')
    $baseRevision = ((Invoke-Git $sourceOriginal @('rev-parse', 'HEAD')) -join '').Trim()

    $sourceChanged = Join-Path $proofRoot 'source-changed'
    $null = Invoke-Git $proofRoot @('clone', '--quiet', $sourceOriginal, $sourceChanged)
    $null = Invoke-Git $sourceChanged @('config', 'user.name', 'OCCT benchmark fixture')
    $null = Invoke-Git $sourceChanged @('config', 'user.email', 'fixture@example.invalid')
    Add-Content -LiteralPath (Join-Path $sourceChanged 'Program.cs') -Value '// harmless generator-host delta'
    $null = Invoke-Git $sourceChanged @('add', 'Program.cs')
    $null = Invoke-Git $sourceChanged @('commit', '--quiet', '-m', 'fixture change')
    $changedRevision = ((Invoke-Git $sourceChanged @('rev-parse', 'HEAD')) -join '').Trim()
    $patchLines = @(Invoke-Git $sourceChanged @('diff', '--binary', '--full-index', '--no-ext-diff', $baseRevision, $changedRevision, '--'))
    $patchPath = Join-Path $proofRoot 'generator-change.patch'
    Write-TextFile $patchPath (($patchLines -join "`n") + "`n")

    $originalSeed = Join-Path $proofRoot 'seed-original'
    $changedSeed = Join-Path $proofRoot 'seed-changed'
    & $dotnet build (Join-Path $sourceOriginal 'Fixture.csproj') -c Release -o $originalSeed --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'The lightweight original managed-host fixture failed to build.' }
    & $dotnet build (Join-Path $sourceChanged 'Fixture.csproj') -c Release -o $changedSeed --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw 'The lightweight changed managed-host fixture failed to build.' }

    $receipts = [ordered]@{
        baselineOriginal = New-HostReceiptFixture bo baseline original $sourceOriginal $baseRevision $originalSeed $dotnet (Join-Path $sourceOriginal 'Fixture.csproj') $null
        baselineChanged = New-HostReceiptFixture bc baseline changed $sourceChanged $baseRevision $changedSeed $dotnet (Join-Path $sourceChanged 'Fixture.csproj') $patchPath
        candidateOriginal = New-HostReceiptFixture co candidate original $sourceOriginal $baseRevision $originalSeed $dotnet (Join-Path $sourceOriginal 'Fixture.csproj') $null
        candidateChanged = New-HostReceiptFixture cc candidate changed $sourceChanged $baseRevision $changedSeed $dotnet (Join-Path $sourceChanged 'Fixture.csproj') $patchPath
    }

    $baselineInput = Join-Path $proofRoot 'in-base'
    $candidateInput = Join-Path $proofRoot 'in-cand'
    $vcpkgToolchain = Join-Path $proofRoot 'toolchain'
    foreach ($path in @($baselineInput, $candidateInput)) {
        Write-TextFile (Join-Path $path 'installed/x64-windows/include/opencascade/Fixture.hxx') "enum class Fixture { Original };`n"
        Write-TextFile (Join-Path $path 'installed/x64-windows/include/opencascade/Stable.hxx') "struct Stable {};`n"
        Write-TextFile (Join-Path $path 'installed/vcpkg/status') "Package: occt`nVersion: fixture`n"
    }
    Write-TextFile (Join-Path $vcpkgToolchain 'scripts/buildsystems/vcpkg.cmake') '# fixture toolchain'
    Write-TextFile (Join-Path $vcpkgToolchain 'installed/vcpkg/status') "Package: occt`nVersion: fixture`n"
    $changedHeader = Join-Path $proofRoot 'Fixture.changed.hxx'
    Write-TextFile $changedHeader "enum class Fixture { Original, AddedForBenchmark };`n"

    $canonicalRoot = Join-Path $proofRoot 'canonical-root'
    $declarationRoot = Join-Path $proofRoot 'declaration-root'
    foreach ($root in @($canonicalRoot, $declarationRoot)) {
        Write-TextFile (Join-Path $root 'generated/csharp/Fixture.g.cs') '// original'
        Write-TextFile (Join-Path $root 'generated/cpp/Fixture.cpp') '// native fixture'
        Write-TextFile (Join-Path $root 'generated/cpp/NativeFunctionTable.cpp') @'
// <auto-generated />
#include <cstdint>

extern "C" void Fixture_One();

static const std::uintptr_t Functions[] =
{
    reinterpret_cast<std::uintptr_t>(&Fixture_One),
};

extern "C" __declspec(dllexport) const std::uintptr_t* NativeApi_GetFunctionTable() noexcept
{
    return Functions;
}
'@
        Write-TextFile (Join-Path $root 'generated/unsupported-headers.txt') "fixture`n"
        Write-TextFile (Join-Path $root 'native-build/Release/ted_toolkit_occt.dll') 'fixture native boundary'
    }
    Write-TextFile (Join-Path $declarationRoot 'generated/csharp/Fixture.g.cs') '// declaration edit'
    $originalManifest = Join-Path $proofRoot 'canonical-original.json'
    $declarationManifest = Join-Path $proofRoot 'canonical-declaration.json'
    & $manifestTool -Roots @((Join-Path $canonicalRoot 'generated/csharp'), (Join-Path $canonicalRoot 'generated/cpp')) `
        -Files (Join-Path $canonicalRoot 'generated/unsupported-headers.txt') -ReportPath $originalManifest
    & $manifestTool -Roots @((Join-Path $declarationRoot 'generated/csharp'), (Join-Path $declarationRoot 'generated/cpp')) `
        -Files (Join-Path $declarationRoot 'generated/unsupported-headers.txt') -ReportPath $declarationManifest
    $originalExports = Join-Path $proofRoot 'exports-original.json'
    $declarationExports = Join-Path $proofRoot 'exports-declaration.json'
    & $exportTool -SourcePath (Join-Path $canonicalRoot 'generated/cpp/NativeFunctionTable.cpp') -ReportPath $originalExports
    & $exportTool -SourcePath (Join-Path $declarationRoot 'generated/cpp/NativeFunctionTable.cpp') -ReportPath $declarationExports

    $gateScript = Join-Path $proofRoot 'native-gate-fixture.ps1'
    Write-TextFile $gateScript 'param([string] $ArtifactRoot, [string] $Variant, [string] $Workload, [string] $Phase); if (-not (Test-Path $ArtifactRoot)) { exit 23 }'
    $powerShellPath = (Get-Process -Id $PID).Path
    $gateSpecification = Join-Path $proofRoot 'native-gate.json'
    Write-JsonFile $gateSpecification ([ordered]@{
        SchemaVersion = 1; FixtureOnly = $true; Executable = $powerShellPath
        Arguments = @('-NoProfile', '-File', $gateScript, '{ArtifactRoot}', '{Variant}', '{Workload}', '{Phase}')
        WorkingDirectory = $proofRoot
        InputFiles = @(@{ Path = $gateScript; Sha256 = (Get-FileHash $gateScript).Hash })
    })

    $arguments = @{
        SpecificationDirectory = Join-Path $proofRoot 'specification'
        BaselineRepositoryRoot = $baselineRepository; CandidateRepositoryRoot = $repository
        BaselineArtifactRoot = Join-Path $proofRoot 'artifact-b'; CandidateArtifactRoot = Join-Path $proofRoot 'artifact-c'
        BaselineInputVcpkgRoot = $baselineInput; CandidateInputVcpkgRoot = $candidateInput
        ToolchainVcpkgRoot = $vcpkgToolchain
        BaselineOriginalHostReceipt = $receipts.baselineOriginal; BaselineChangedHostReceipt = $receipts.baselineChanged
        CandidateOriginalHostReceipt = $receipts.candidateOriginal; CandidateChangedHostReceipt = $receipts.candidateChanged
        CanonicalArtifactRoot = $canonicalRoot; CanonicalOriginalManifest = $originalManifest
        CanonicalDeclarationManifest = $declarationManifest; CanonicalOriginalExportInventory = $originalExports
        CanonicalDeclarationExportInventory = $declarationExports; NativeGateSpecification = $gateSpecification
        DeclarationHeaderRelativePath = 'opencascade/Fixture.hxx'; DeclarationChangedFile = $changedHeader
        MissingOutputRelativePath = 'cpp/Fixture.cpp'; DotNetPath = $dotnet; CMakePath = $toolchain.CMake
        NinjaPath = $toolchain.Ninja; CompilerPath = $toolchain.Compiler; VcVarsPath = $toolchain.VcVars
        DeadlineUtc = [DateTimeOffset]::UtcNow.AddHours(2); MemoryLimitBytes = 1073741824; MemoryReserveBytes = 1073741824
    }
    & $builder @arguments
    $specification = $arguments.SpecificationDirectory
    $plan = Get-Content -LiteralPath (Join-Path $specification 'occt-plan.json') -Raw | ConvertFrom-Json
    $matrix = Get-Content -LiteralPath (Join-Path $specification 'matrix.json') -Raw | ConvertFrom-Json
    Require $plan.FixtureOnly 'Synthetic receipts did not force a fixture-only plan.'
    Require ($plan.HarnessBinding -ceq 'commit:PENDING-FINAL-HARNESS-COMMIT') 'Pending harness binding was lost.'
    Require ($plan.ToolchainSnapshot.CompilerBv.Length -gt 0 -and $plan.ToolchainSnapshot.WindowsSDKVersion.Length -gt 0) 'Exact vcvars/compiler/SDK identity was not pinned.'
    Require ($plan.ArtifactVolumeIdentity.Length -gt 0) 'Artifact physical volume was not pinned.'
    Require ($plan.NativeGate.SpecificationPath -ceq $gateSpecification) 'Native boundary gate was not bound.'
    Require ((@($matrix.Workloads.Name) -join ',') -ceq 'artifact-cold,unchanged,declaration-edit,generator-change,missing-output') 'Workload inventory drifted.'
    foreach ($workload in $matrix.Workloads) {
        foreach ($variant in @('baseline', 'candidate')) {
            $measure = @($workload.$variant.Measure | ForEach-Object { Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json })
            $actions = @($measure | ForEach-Object { $_.Arguments[$_.Arguments.IndexOf('-Action') + 1] })
            $expected = if ($workload.Name -eq 'artifact-cold') { 'Generate,Configure,Build' } else { 'Generate,Build' }
            Require (($actions -join ',') -ceq $expected) "Wrong measured stages: $($workload.Name)/$variant"
        }
    }
    & $matrixRunner -SpecificationPath (Join-Path $specification 'matrix.json') `
        -ReportDirectory (Join-Path $proofRoot 'matrix-plan-only') -PlanOnly

    $invalidHostRoot = Join-Path $proofRoot 'hosts-invalid'
    Write-TextFile (Join-Path $invalidHostRoot 'TedToolkit.CppBindings.Occt.Console.dll') 'not a managed PE'
    Write-TextFile (Join-Path $invalidHostRoot 'TedToolkit.CppBindings.Occt.Console.deps.json') '{}'
    Write-TextFile (Join-Path $invalidHostRoot 'TedToolkit.CppBindings.Occt.Console.runtimeconfig.json') '{}'
    $invalidReceipt = Get-Content -LiteralPath $receipts.baselineOriginal -Raw | ConvertFrom-Json -AsHashtable
    $invalidReceipt.HostDirectory = $invalidHostRoot
    $invalidReceipt.HostFiles = @(Get-ChildItem $invalidHostRoot -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = $_.Name; Bytes = $_.Length; Sha256 = (Get-FileHash $_.FullName).Hash }
    })
    $invalidReceiptPath = Join-Path $proofRoot 'invalid-host-receipt.json'
    Write-JsonFile $invalidReceiptPath $invalidReceipt
    $invalidArguments = $arguments.Clone(); $invalidArguments.SpecificationDirectory = Join-Path $proofRoot 'invalid-host-spec'
    $invalidArguments.BaselineOriginalHostReceipt = $invalidReceiptPath
    $invalidRejected = $false
    try { & $builder @invalidArguments }
    catch { $invalidRejected = $_.Exception.Message -like '*valid managed assembly*' }
    Require $invalidRejected 'A dummy text DLL satisfied host provenance.'

    $caseArguments = $arguments.Clone(); $caseArguments.SpecificationDirectory = Join-Path $proofRoot 'case-spec'
    $caseArguments.CandidateArtifactRoot = $arguments.BaselineArtifactRoot.ToUpperInvariant()
    $caseRejected = $false
    try { & $builder @caseArguments }
    catch { $caseRejected = $_.Exception.Message -like '*overlap*case*' -or $_.Exception.Message -like '*must be isolated*' }
    Require $caseRejected 'Case-insensitive shared artifact roots were accepted.'

    $junctionTarget = Join-Path $proofRoot 'junction-target'
    Copy-Item -LiteralPath $baselineInput -Destination $junctionTarget -Recurse
    $junctionInput = Join-Path $proofRoot 'junction-linkxx'
    Require ($junctionInput.Length -eq $junctionTarget.Length) 'Junction isolation fixture paths must have equal measured length.'
    $null = New-Item -ItemType Junction -Path $junctionInput -Target $junctionTarget
    $junctionArguments = $arguments.Clone(); $junctionArguments.SpecificationDirectory = Join-Path $proofRoot 'junction-spec'
    $junctionArguments.BaselineInputVcpkgRoot = $junctionInput; $junctionArguments.CandidateInputVcpkgRoot = $junctionTarget
    $junctionRejected = $false
    try { & $builder @junctionArguments }
    catch { $junctionRejected = $_.Exception.Message -like '*reparse*' -or $_.Exception.Message -like '*overlap*' }
    Require $junctionRejected 'A junction/shared physical input target was accepted.'

    $executablePlanPath = Join-Path $specification 'occt-plan-executable-fixture.json'
    $executablePlan = Get-Content -LiteralPath (Join-Path $specification 'occt-plan.json') -Raw | ConvertFrom-Json -AsHashtable
    $executablePlan.FixtureOnly = $false
    $executablePlan.HarnessBinding = "commit:$($executablePlan.CandidateHead)"
    Write-JsonFile $executablePlanPath $executablePlan
    $nestedTarget = Join-Path $proofRoot 'nested-target'; $null = [IO.Directory]::CreateDirectory($nestedTarget)
    $nestedJunction = Join-Path $arguments.BaselineArtifactRoot 'nested/reparse'
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($nestedJunction))
    $null = New-Item -ItemType Junction -Path $nestedJunction -Target $nestedTarget
    $nestedRejected = $false
    try { & $adapter -PlanPath $executablePlanPath -Action Prepare -Variant baseline -Workload artifact-cold -SampleRoot $proofRoot }
    catch { $nestedRejected = $_.Exception.Message -like '*nested reparse point*' }
    Require $nestedRejected 'A nested artifact junction reached cleanup.'

    Write-Output "OCCT benchmark plan proof passed: managed host receipts, exact patch provenance, physical isolation, volume/path shape, vcvars compiler identity, canonical oracles, native gate, five workloads, and plan-only guards. Evidence: $proofRoot"
}
finally {
    if (Test-Path -LiteralPath $proofRoot) { Remove-Item -LiteralPath $proofRoot -Recurse -Force }
}
