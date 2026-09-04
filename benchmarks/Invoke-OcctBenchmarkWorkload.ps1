#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $PlanPath,
    [Parameter(Mandatory)] [ValidateSet('Prepare', 'ValidateGenerationToolchain', 'Generate', 'Configure', 'Build', 'Verify', 'Settle')] [string] $Action,
    [Parameter(Mandatory)] [ValidateSet('baseline', 'candidate')] [string] $Variant,
    [Parameter(Mandatory)] [ValidateSet('artifact-cold', 'unchanged', 'declaration-edit', 'generator-change', 'missing-output')] [string] $Workload,
    [Parameter(Mandatory)] [string] $SampleRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'BenchmarkPath.ps1')

function Read-Json {
    param([string] $Path)
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable -DateKind String
}

function Write-NewJson {
    param([string] $Path, $Value)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    $bytes = $utf8.GetBytes(($Value | ConvertTo-Json -Depth 20))
    $stream = [IO.File]::Open($Path, 'CreateNew', 'Write', 'None')
    try { $stream.Write($bytes) }
    finally { $stream.Dispose() }
}

function Assert-Hash {
    param([string] $Path, [string] $Expected)
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $Expected) {
        throw "Frozen benchmark input changed: $Path"
    }
}

function Assert-PrivateInputFile {
    param([string] $Path, [string] $ExpectedIdentity, [string] $Label)
    $identity = Get-BenchmarkFileIdentity $Path
    if ($identity.LinkCount -ne 1) {
        throw "$Label must remain a private file with hard-link count 1: $Path"
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedIdentity) -or $identity.Identity -cne $ExpectedIdentity) {
        throw "$Label physical file identity changed; rebaseline before sampling."
    }
}

function Assert-FrozenBindings {
    foreach ($binding in $plan.FrozenFileBindings) {
        Assert-Hash $binding.Path $binding.Sha256
    }
    if ($plan.HostPublishBindings -isnot [array] -or $plan.HostPublishBindings.Count -ne 4) {
        throw 'The plan must retain all four host publish bindings.'
    }
    $dotnetPath = Resolve-BenchmarkPhysicalPath $plan.Tools.DotNet
    $dotnetSha256 = (Get-FileHash -LiteralPath $dotnetPath -Algorithm SHA256).Hash
    foreach ($binding in $plan.HostPublishBindings) {
        $publish = Read-Json $binding.PublishCompletionReceiptPath
        if (-not (Test-BenchmarkPathEqual $binding.DotNetPath $dotnetPath) -or
            $binding.DotNetSha256 -cne $dotnetSha256 -or
            -not (Test-BenchmarkPathEqual $publish.DotNetPath $dotnetPath) -or
            $publish.DotNetSha256 -cne $dotnetSha256 -or
            -not (Test-BenchmarkPathEqual $publish.Command.Executable $dotnetPath)) {
            throw 'A frozen Console host publish no longer matches the plan-resolved dotnet path and SHA-256.'
        }
    }
}

function Assert-WithinRoot {
    param([string] $Path, [string] $Root)
    $fullPath = Resolve-BenchmarkPhysicalPath $Path
    $fullRoot = (Resolve-BenchmarkPhysicalPath $Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes its isolated benchmark root: $fullPath"
    }
    return $fullPath
}

function Assert-NoReparseComponents {
    param([string] $Path)
    foreach ($candidate in @([IO.Path]::GetFullPath($Path), (Resolve-BenchmarkPhysicalPath $Path))) {
        $current = $candidate
        while (-not [string]::IsNullOrEmpty($current)) {
            if (Test-Path -LiteralPath $current) {
                $item = Get-Item -LiteralPath $current -Force
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw "Benchmark paths cannot contain reparse-point components: $($item.FullName)"
                }
            }
            $parent = [IO.Path]::GetDirectoryName($current)
            if ([string]::IsNullOrEmpty($parent) -or $parent -ceq $current) { break }
            $current = $parent
        }
    }
}

function Assert-NoNestedReparse {
    param([string] $Root)
    $reparse = Get-ChildItem -LiteralPath $Root -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $reparse) { throw "Benchmark tree contains a nested reparse point: $($reparse.FullName)" }
}

function Assert-IsolatedRoots {
    $roots = [ordered]@{
        baselineRepository = $plan.Variants.baseline.RepositoryRoot
        candidateRepository = $plan.Variants.candidate.RepositoryRoot
        baselineInput = $plan.Variants.baseline.InputVcpkgRoot
        candidateInput = $plan.Variants.candidate.InputVcpkgRoot
        toolchainVcpkg = $plan.ToolchainVcpkgRoot
        baselineArtifact = $plan.Variants.baseline.ArtifactRoot
        candidateArtifact = $plan.Variants.candidate.ArtifactRoot
        canonicalArtifact = $plan.Canonical.ArtifactRoot
        baselineOriginalHost = $plan.Variants.baseline.OriginalHostRoot
        baselineChangedHost = $plan.Variants.baseline.ChangedHostRoot
        candidateOriginalHost = $plan.Variants.candidate.OriginalHostRoot
        candidateChangedHost = $plan.Variants.candidate.ChangedHostRoot
    }
    $names = @($roots.Keys)
    for ($leftIndex = 0; $leftIndex -lt $names.Count; $leftIndex++) {
        $left = (Resolve-BenchmarkPhysicalPath $roots[$names[$leftIndex]]).TrimEnd('\', '/')
        Assert-NoReparseComponents $left
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $names.Count; $rightIndex++) {
            $right = (Resolve-BenchmarkPhysicalPath $roots[$names[$rightIndex]]).TrimEnd('\', '/')
            if ($left.Equals($right, [StringComparison]::OrdinalIgnoreCase) -or
                $left.StartsWith($right + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                $right.StartsWith($left + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Benchmark roots overlap physically or by case: $($names[$leftIndex]) / $($names[$rightIndex])"
            }
        }
    }
}

function Assert-RootMarker {
    param([hashtable] $VariantPlan)
    $markerPath = Join-Path $VariantPlan.ArtifactRoot '.occt-benchmark-root.json'
    $marker = Read-Json $markerPath
    if ($marker.SchemaVersion -ne 1 -or $marker.PlanId -cne $plan.PlanId -or $marker.Variant -cne $Variant) {
        throw 'Artifact-root ownership marker does not match the frozen plan.'
    }
    if ((Get-Item -LiteralPath $VariantPlan.ArtifactRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Artifact roots cannot be reparse points.'
    }
}

function Assert-FrozenInputs {
    param([hashtable] $VariantPlan, [ValidateSet('original', 'changed')] [string] $HeaderState)
    $manifest = Read-Json $plan.FrozenInputManifest
    if ($manifest.SchemaVersion -ne 2 -or
        $manifest.TripletRelativePath -cne "installed/$($plan.Triplet)" -or
        $manifest.StatusRelativePath -cne 'installed/vcpkg/status') {
        throw 'The complete private-vcpkg input manifest contract changed.'
    }
    Assert-NoReparseComponents $VariantPlan.InputVcpkgRoot
    $expectedTripletRoot = Assert-WithinRoot (Join-Path $VariantPlan.InputVcpkgRoot $manifest.TripletRelativePath) `
        $VariantPlan.InputVcpkgRoot
    $expectedStatusFile = Assert-WithinRoot (Join-Path $VariantPlan.InputVcpkgRoot $manifest.StatusRelativePath) `
        $VariantPlan.InputVcpkgRoot
    if (-not (Test-BenchmarkPathEqual $expectedTripletRoot $VariantPlan.TripletRoot) -or
        -not (Test-BenchmarkPathEqual $expectedStatusFile $VariantPlan.StatusFile)) {
        throw 'The private triplet or vcpkg status path changed.'
    }
    Assert-NoReparseComponents $VariantPlan.TripletRoot
    Assert-NoReparseComponents $VariantPlan.StatusFile
    $reparse = Get-ChildItem -LiteralPath $VariantPlan.TripletRoot -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($null -ne $reparse) { throw "Private input tree contains a reparse point: $($reparse.FullName)" }
    foreach ($requiredDirectory in @('include', 'lib', 'bin')) {
        $path = Assert-WithinRoot (Join-Path $VariantPlan.TripletRoot $requiredDirectory) $VariantPlan.InputVcpkgRoot
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Private triplet directory is missing: $requiredDirectory"
        }
    }
    $entries = @($manifest.Entries | Where-Object Variant -CEQ $Variant)
    $actual = @(
        Get-ChildItem -LiteralPath $VariantPlan.TripletRoot -Recurse -File
        Get-Item -LiteralPath $VariantPlan.StatusFile
    )
    if ($actual.Count -ne $entries.Count + 1) { throw 'Private vcpkg input inventory changed; rebaseline.' }
    foreach ($entry in $entries) {
        $path = Assert-WithinRoot (Join-Path $VariantPlan.InputVcpkgRoot $entry.Path) $VariantPlan.InputVcpkgRoot
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item $path).Length -ne $entry.Bytes) {
            throw "Private vcpkg input inventory changed: $($entry.Path)"
        }
        Assert-PrivateInputFile $path $entry.FileIdentity 'Private vcpkg input'
        Assert-Hash $path $entry.Sha256
    }
    $expectedHeaderHash = if ($HeaderState -eq 'original') { $manifest.OriginalHeaderSha256 } else { $manifest.ChangedHeaderSha256 }
    Assert-PrivateInputFile $VariantPlan.HeaderPath $VariantPlan.HeaderFileIdentity 'Mutable declaration header'
    Assert-PrivateInputFile $VariantPlan.StatusFile $VariantPlan.StatusFileIdentity 'Private vcpkg status file'
    Assert-Hash $VariantPlan.HeaderPath $expectedHeaderHash
    Assert-Hash $VariantPlan.StatusFile $VariantPlan.StatusFileSha256
}

function Assert-MutablePrivateInputs {
    param([hashtable] $VariantPlan, [string] $ExpectedHeaderHash)
    Assert-PrivateInputFile $VariantPlan.HeaderPath $VariantPlan.HeaderFileIdentity 'Mutable declaration header'
    Assert-PrivateInputFile $VariantPlan.StatusFile $VariantPlan.StatusFileIdentity 'Private vcpkg status file'
    Assert-Hash $VariantPlan.HeaderPath $ExpectedHeaderHash
    Assert-Hash $VariantPlan.StatusFile $VariantPlan.StatusFileSha256
}

function Assert-RepositoryRevision {
    param([hashtable] $VariantPlan)
    $expected = if ($Variant -eq 'baseline') { $plan.BaselineRevision } else { $plan.CandidateHead }
    $head = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" `
        -C $VariantPlan.RepositoryRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($head -join '').Trim() -cne $expected) {
        throw "Repository revision changed for $Variant."
    }
    $status = @(& git -c "safe.directory=$($VariantPlan.RepositoryRoot)" `
        -C $VariantPlan.RepositoryRoot status --porcelain=v1 --untracked-files=all --ignore-submodules=all 2>$null)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) { throw "Repository is not clean for $Variant." }
}

function Invoke-Checked {
    param([string] $Executable, [string[]] $Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable exited with code $LASTEXITCODE." }
}

function Use-CompilerEnvironment {
    $lines = @(& $env:COMSPEC /d /c "call `"$($plan.Tools.VcVars)`" >nul && set")
    if ($LASTEXITCODE -ne 0) { throw 'The pinned MSVC environment could not be initialized.' }
    $previous = @{}
    foreach ($line in $lines) {
        if ($line -match '^([^=]+)=(.*)$') {
            $previous[$Matches[1]] = [Environment]::GetEnvironmentVariable($Matches[1], 'Process')
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
        }
    }
    return $previous
}

function Restore-Environment {
    param([hashtable] $Previous)
    foreach ($entry in $Previous.GetEnumerator()) {
        $value = if ($null -eq $entry.Value) { [NullString]::Value } else { $entry.Value }
        [Environment]::SetEnvironmentVariable($entry.Key, $value, 'Process')
    }
}

function Get-StringSha256 {
    param([string] $Value)

    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($utf8.GetBytes($Value)))
}

function Use-GenerationEnvironment {
    param([string] $VcpkgRoot)

    $previous = @{
        PATH = [Environment]::GetEnvironmentVariable('PATH', 'Process')
        VCPKG_ROOT = [Environment]::GetEnvironmentVariable('VCPKG_ROOT', 'Process')
    }
    [Environment]::SetEnvironmentVariable('PATH', $plan.ToolchainSnapshot.ClangGenerationPath, 'Process')
    [Environment]::SetEnvironmentVariable('VCPKG_ROOT', $VcpkgRoot, 'Process')
    return $previous
}

function Get-ClangDriverSelection {
    param([string] $Trace)

    $unescaped = $Trace.Replace('\\', '\')
    $msvcMatches = [regex]::Matches($unescaped,
        '(?i)(?<root>[A-Z]:\\[^"\r\n]+?\\VC\\Tools\\MSVC\\(?<version>[^\\"]+))\\(?:include|lib)')
    $sdkMatches = [regex]::Matches($unescaped,
        '(?i)(?<root>[A-Z]:\\[^"\r\n]+?\\Windows Kits\\10)\\(?:include|lib)\\(?<version>[^\\"]+)')
    $msvcSelections = @($msvcMatches | ForEach-Object {
        "$($_.Groups['root'].Value)|$($_.Groups['version'].Value)"
    } | Sort-Object -Unique)
    $sdkSelections = @($sdkMatches | ForEach-Object {
        "$($_.Groups['root'].Value)|$($_.Groups['version'].Value)"
    } | Sort-Object -Unique)
    if ($msvcSelections.Count -ne 1 -or $sdkSelections.Count -ne 1) {
        throw 'The Clang driver trace did not select exactly one MSVC and Windows SDK toolchain.'
    }
    $msvc = $msvcSelections[0].Split('|', 2)
    $sdk = $sdkSelections[0].Split('|', 2)
    return [ordered]@{
        MsvcRoot = Resolve-BenchmarkPhysicalPath $msvc[0]
        MsvcVersion = $msvc[1]
        WindowsSdkRoot = Resolve-BenchmarkPhysicalPath $sdk[0]
        WindowsSdkVersion = $sdk[1]
    }
}

function Get-ClangDriverCompanionPaths {
    param([string] $Trace)

    $paths = [Collections.Generic.List[string]]::new()
    foreach ($line in $Trace -split "`n") {
        if ($line -notmatch '^\s*"(?<path>[^"]+)"') { continue }
        $path = $Matches.path.Replace('\\', '\')
        if (-not [IO.Path]::HasExtension($path) -and (Test-Path -LiteralPath "$path.exe" -PathType Leaf)) {
            $path = "$path.exe"
        }
        $resolved = Resolve-BenchmarkPhysicalPath $path
        if (-not (Test-BenchmarkPathEqual $resolved $plan.Tools.Clang) -and $resolved -cnotin $paths) {
            $paths.Add($resolved)
        }
    }
    return @($paths)
}

function Assert-ClangResourceInventory {
    $snapshot = $plan.ToolchainSnapshot
    Assert-Hash $snapshot.ClangResourceInventoryPath $snapshot.ClangResourceInventorySha256
    $manifest = Read-Json $snapshot.ClangResourceInventoryPath
    if ($manifest.SchemaVersion -ne 1 -or
        -not (Test-BenchmarkPathEqual $manifest.ResourceDirectory $snapshot.ClangResourceDirectory) -or
        $manifest.FileCount -ne $snapshot.ClangResourceFileCount -or
        $manifest.TotalBytes -ne $snapshot.ClangResourceTotalBytes -or
        $manifest.Files -isnot [array] -or $manifest.Files.Count -eq 0) {
        throw 'The frozen Clang resource-directory inventory is invalid.'
    }
    Assert-NoReparseComponents $snapshot.ClangResourceDirectory
    Assert-NoNestedReparse $snapshot.ClangResourceDirectory
    $actual = @(Get-ChildItem -LiteralPath $snapshot.ClangResourceDirectory -Recurse -File |
        Sort-Object { [IO.Path]::GetRelativePath($snapshot.ClangResourceDirectory, $_.FullName).Replace('\', '/') } -CaseSensitive)
    $actualTotalBytes = ($actual | Measure-Object Length -Sum).Sum
    if ($actual.Count -ne $manifest.Files.Count -or $actualTotalBytes -ne $manifest.TotalBytes) {
        throw 'The Clang resource-directory inventory changed.'
    }
    for ($index = 0; $index -lt $actual.Count; $index++) {
        $expected = $manifest.Files[$index]
        $relative = [IO.Path]::GetRelativePath($snapshot.ClangResourceDirectory, $actual[$index].FullName).Replace('\', '/')
        if ($relative -cne $expected.Path -or $actual[$index].Length -ne $expected.Bytes -or
            (Get-FileHash -LiteralPath $actual[$index].FullName -Algorithm SHA256).Hash -cne $expected.Sha256) {
            throw "The Clang resource directory changed: $relative"
        }
    }
}

function Assert-ClangDriverSnapshot {
    $snapshot = $plan.ToolchainSnapshot
    $requiredDriverArguments = @('-###', '-fdebug-compilation-dir=.', '-fcoverage-compilation-dir=.',
        '-save-temps=obj', '-x', 'c++', 'NUL', '-o', 'NUL.exe')
    if (($snapshot.ClangDriverTraceArguments -join "`0") -cne ($requiredDriverArguments -join "`0")) {
        throw 'The stable Clang driver trace arguments changed.'
    }
    if (-not (Test-BenchmarkPathEqual $plan.Tools.Clang $snapshot.ClangPath)) {
        throw 'The generation environment selected a different clang++ executable.'
    }
    Assert-Hash $plan.Tools.Clang $snapshot.ClangSha256
    $resolvedClang = (Get-Command clang++ -CommandType Application -ErrorAction Stop).Source
    if (-not (Test-BenchmarkPathEqual $resolvedClang $plan.Tools.Clang)) {
        throw 'The generation environment resolved a different clang++ executable.'
    }
    $clangVersion = @(& $plan.Tools.Clang --version 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($clangVersion -join "`n") -cne $snapshot.ClangVersion) {
        throw 'The pinned clang++ version identity changed.'
    }
    $driverTraceLines = @(& $plan.Tools.Clang @($snapshot.ClangDriverTraceArguments) 2>&1)
    $driverTrace = $driverTraceLines -join "`n"
    if ($LASTEXITCODE -ne 0 -or $driverTrace -cne $snapshot.ClangDriverTrace -or
        (Get-StringSha256 $driverTrace) -cne $snapshot.ClangDriverTraceSha256) {
        throw 'The effective Clang driver trace changed.'
    }
    $resourceLines = @(& $plan.Tools.Clang -print-resource-dir 2>&1)
    if ($LASTEXITCODE -ne 0 -or $resourceLines.Count -ne 1 -or
        -not (Test-BenchmarkPathEqual $resourceLines[0].Trim() $snapshot.ClangResourceDirectory)) {
        throw 'The selected Clang resource directory changed.'
    }
    $actualCompanions = @(Get-ClangDriverCompanionPaths $driverTrace)
    if ($snapshot.ClangDriverCompanions -isnot [array] -or
        @($snapshot.ClangDriverCompanions | Where-Object {
            [IO.Path]::GetFileName($_.Path) -ceq 'lld-link.exe'
        }).Count -ne 1 -or $actualCompanions.Count -ne $snapshot.ClangDriverCompanions.Count) {
        throw 'The Clang driver companion inventory changed.'
    }
    for ($index = 0; $index -lt $actualCompanions.Count; $index++) {
        $expected = $snapshot.ClangDriverCompanions[$index]
        if (-not (Test-BenchmarkPathEqual $actualCompanions[$index] $expected.Path)) {
            throw 'The Clang driver companion inventory changed.'
        }
        if ((Get-FileHash -LiteralPath $expected.Path -Algorithm SHA256).Hash -cne $expected.Sha256) {
            throw "The Clang driver companion changed: $($expected.Path)"
        }
    }
    $selection = Get-ClangDriverSelection $driverTrace
    if (-not (Test-BenchmarkPathEqual $selection.MsvcRoot $snapshot.ClangSelectedMsvcRoot) -or
        $selection.MsvcVersion -cne $snapshot.ClangSelectedMsvcVersion -or
        -not (Test-BenchmarkPathEqual $selection.WindowsSdkRoot $snapshot.ClangSelectedWindowsSdkRoot) -or
        $selection.WindowsSdkVersion -cne $snapshot.ClangSelectedWindowsSdkVersion -or
        -not (Test-BenchmarkPathEqual $selection.MsvcRoot $snapshot.VCToolsInstallDir) -or
        $selection.MsvcVersion -cne (Split-Path $snapshot.VCToolsInstallDir.TrimEnd('\', '/') -Leaf) -or
        -not (Test-BenchmarkPathEqual $selection.WindowsSdkRoot $snapshot.WindowsSdkDir) -or
        $selection.WindowsSdkVersion -cne $snapshot.WindowsSDKVersion.TrimEnd('\', '/')) {
        throw 'Clang selected a different MSVC or Windows SDK toolchain than vcvars64.'
    }
    Assert-ClangResourceInventory
}

function Get-GenerationValidationReceiptPath {
    Join-Path $sample "generation-toolchain-$Variant-$Workload.json"
}

function Invoke-GenerationToolchainValidation {
    param([hashtable] $VariantPlan, [bool] $ChangedHost)

    $generatorHost = if ($ChangedHost) { $VariantPlan.ChangedHost } else { $VariantPlan.OriginalHost }
    $expected = if ($ChangedHost) { $VariantPlan.ChangedHostSha256 } else { $VariantPlan.OriginalHostSha256 }
    Assert-Hash $generatorHost $expected
    $generationPrevious = Use-GenerationEnvironment $VariantPlan.InputVcpkgRoot
    try { Assert-ClangDriverSnapshot }
    finally { Restore-Environment $generationPrevious }
    Write-NewJson (Get-GenerationValidationReceiptPath) ([ordered]@{
        SchemaVersion = 1
        PlanId = $plan.PlanId
        Variant = $Variant
        Workload = $Workload
        CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        GeneratorHostSha256 = $expected
        ClangDriverTraceSha256 = $plan.ToolchainSnapshot.ClangDriverTraceSha256
        ClangResourceInventorySha256 = $plan.ToolchainSnapshot.ClangResourceInventorySha256
    })
}

function Assert-ConsumeGenerationValidationReceipt {
    param([hashtable] $VariantPlan, [bool] $ChangedHost)

    $path = Get-GenerationValidationReceiptPath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'Generate requires a fresh pre-measurement toolchain validation receipt.'
    }
    $receipt = Read-Json $path
    $expectedHost = if ($ChangedHost) { $VariantPlan.ChangedHostSha256 } else { $VariantPlan.OriginalHostSha256 }
    $completed = [DateTimeOffset]::Parse($receipt.CompletedAtUtc).ToUniversalTime()
    $age = [DateTimeOffset]::UtcNow - $completed
    if ($receipt.SchemaVersion -ne 1 -or $receipt.PlanId -cne $plan.PlanId -or
        $receipt.Variant -cne $Variant -or $receipt.Workload -cne $Workload -or
        $receipt.GeneratorHostSha256 -cne $expectedHost -or
        $receipt.ClangDriverTraceSha256 -cne $plan.ToolchainSnapshot.ClangDriverTraceSha256 -or
        $receipt.ClangResourceInventorySha256 -cne $plan.ToolchainSnapshot.ClangResourceInventorySha256 -or
        $age -lt [TimeSpan]::Zero -or $age -gt [TimeSpan]::FromSeconds(30)) {
        throw 'Generate requires a fresh matching pre-measurement toolchain validation receipt.'
    }
    Remove-Item -LiteralPath $path -Force
}

function Assert-ActiveCompilerEnvironment {
    $environmentBindings = [ordered]@{
        VCToolsInstallDir = 'VCToolsInstallDir'
        WindowsSdkDir = 'WindowsSdkDir'
        WindowsSDKVersion = 'WindowsSDKVersion'
        HostArchitecture = 'VSCMD_ARG_HOST_ARCH'
        TargetArchitecture = 'VSCMD_ARG_TGT_ARCH'
        Include = 'INCLUDE'
        Lib = 'LIB'
        LibPath = 'LIBPATH'
    }
    foreach ($binding in $environmentBindings.GetEnumerator()) {
        if ([Environment]::GetEnvironmentVariable($binding.Value, 'Process') -cne
            $plan.ToolchainSnapshot[$binding.Key]) {
            throw "vcvars-selected toolchain changed: $($binding.Value)"
        }
    }
    $selectedCompiler = Resolve-BenchmarkPhysicalPath (Join-Path $plan.ToolchainSnapshot.VCToolsInstallDir 'bin/Hostx64/x64/cl.exe')
    if (-not (Test-BenchmarkPathEqual $selectedCompiler $plan.Tools.Compiler)) {
        throw 'VCToolsInstallDir selected a different compiler.'
    }
}

function Invoke-Generation {
    param([hashtable] $VariantPlan, [bool] $ChangedHost)
    $generatorHost = if ($ChangedHost) { $VariantPlan.ChangedHost } else { $VariantPlan.OriginalHost }
    Assert-ConsumeGenerationValidationReceipt $VariantPlan $ChangedHost
    $previous = Use-GenerationEnvironment $VariantPlan.InputVcpkgRoot
    try {
        Invoke-Checked $plan.Tools.DotNet @($generatorHost, '--output-root', $generatedRoot)
    }
    finally { Restore-Environment $previous }
}

function Invoke-NativeBuild {
    $previous = Use-CompilerEnvironment
    try {
        Assert-ActiveCompilerEnvironment
        Invoke-Checked $plan.Tools.CMake @('--build', $nativeBuildRoot, '--config', $plan.Configuration,
            '--parallel', ([string] $plan.Parallelism))
    }
    finally { Restore-Environment $previous }
}

function Get-Manifest {
    param([string] $Path, [string] $CompareTo)
    $arguments = @{
        Roots = @($csharpRoot, $cppRoot)
        Files = @($unsupportedHeaders)
        ReportPath = $Path
    }
    if ($CompareTo) { $arguments.CompareTo = $CompareTo }
    $null = & $plan.Tools.Manifest @arguments
}

function Assert-CompleteArtifacts {
    if (@(Get-ChildItem -LiteralPath $csharpRoot -File -Filter '*.cs').Count -eq 0 -or
        @(Get-ChildItem -LiteralPath $cppRoot -File -Filter '*.cpp').Count -eq 0 -or
        -not (Test-Path -LiteralPath $unsupportedHeaders -PathType Leaf)) {
        throw 'Generated OCCT source inventory is incomplete.'
    }
    $library = Join-Path $nativeBuildRoot "$($plan.Configuration)/ted_toolkit_occt.dll"
    if (-not (Test-Path -LiteralPath $library -PathType Leaf) -or (Get-Item $library).Length -eq 0) {
        throw 'The native build did not produce the expected nonempty binding library.'
    }
}

function Assert-ManifestOracle {
    param($Actual, [string] $OraclePath)
    $oracle = Read-Json $OraclePath
    if ($Actual.FileCount -ne $oracle.FileCount -or $Actual.TotalBytes -ne $oracle.TotalBytes) {
        throw 'Generated artifacts differ from the canonical baseline oracle.'
    }
    for ($index = 0; $index -lt $Actual.Files.Count; $index++) {
        $left = $Actual.Files[$index]
        $right = $oracle.Files[$index]
        if ($left.Path -cne $right.Path -or $left.Bytes -ne $right.Bytes -or $left.Sha256 -cne $right.Sha256) {
            throw "Generated artifacts differ from the canonical baseline oracle at $($left.Path)."
        }
    }
}

function Expand-NativeGateArguments {
    param([string] $ArtifactRoot, [string] $Phase)
    $values = @{
        '{ArtifactRoot}' = $ArtifactRoot
        '{GeneratedRoot}' = Join-Path $ArtifactRoot 'generated'
        '{NativeBuildRoot}' = Join-Path $ArtifactRoot 'native-build'
        '{Variant}' = $Variant
        '{Workload}' = $Workload
        '{Phase}' = $Phase
    }
    foreach ($argument in $plan.NativeGate.Arguments) {
        if ($values.ContainsKey($argument)) { $values[$argument] } else { $argument }
    }
}

function Invoke-NativeGate {
    param([string] $ArtifactRoot, [string] $Phase)
    Push-Location $plan.NativeGate.WorkingDirectory
    try { Invoke-Checked $plan.NativeGate.Executable @(Expand-NativeGateArguments $ArtifactRoot $Phase) }
    finally { Pop-Location }
}

function Assert-CanonicalBoundary {
    $manifestPath = Join-Path $sample 'canonical-pre-artifacts.json'
    $canonicalGenerated = Join-Path $plan.Canonical.ArtifactRoot 'generated'
    $jsonOptions = [Text.Json.JsonSerializerOptions]::new()
    $rootsJson = [Text.Json.JsonSerializer]::Serialize([string[]]@(
        (Join-Path $canonicalGenerated 'csharp'), (Join-Path $canonicalGenerated 'cpp')), $jsonOptions)
    $filesJson = [Text.Json.JsonSerializer]::Serialize([string[]]@(
        (Join-Path $canonicalGenerated 'unsupported-headers.txt')), $jsonOptions)
    $arguments = @('-NoProfile', '-File', $plan.Tools.Manifest, '-RootsJson', $rootsJson,
        '-FilesJson', $filesJson, '-ReportPath', $manifestPath)
    Invoke-Checked (Get-Process -Id $PID).Path $arguments
    Assert-ManifestOracle (Read-Json $manifestPath) $plan.Canonical.OriginalManifest
    $exportsPath = Join-Path $sample 'canonical-pre-exports.json'
    Invoke-Checked (Get-Process -Id $PID).Path @('-NoProfile', '-File', $plan.Tools.ExportInventory,
        '-SourcePath', (Join-Path $canonicalGenerated 'cpp/NativeFunctionTable.cpp'),
        '-ReportPath', $exportsPath, '-CompareTo', $plan.Canonical.OriginalExportInventory)
    if (-not (Read-Json $exportsPath).Comparison.EqualOrderedExports) {
        throw 'The immutable canonical export boundary changed.'
    }
}

function Assert-ToolchainSnapshot {
    $previous = Use-CompilerEnvironment
    try {
        Assert-ActiveCompilerEnvironment
        $compilerBv = @(& $plan.Tools.Compiler /Bv /c NUL 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($compilerBv -join "`n") -cne $plan.ToolchainSnapshot.CompilerBv) {
            throw 'The selected compiler /Bv identity changed.'
        }
        $cmakeVersion = @(& $plan.Tools.CMake --version 2>&1)
        $ninjaVersion = @(& $plan.Tools.Ninja --version 2>&1)
        $dotnetInfo = @(& $plan.Tools.DotNet --info 2>&1)
        if (($cmakeVersion -join "`n") -cne $plan.ToolchainSnapshot.CMakeVersion -or
            ($ninjaVersion -join "`n") -cne $plan.ToolchainSnapshot.NinjaVersion -or
            ($dotnetInfo -join "`n") -cne $plan.ToolchainSnapshot.DotNetInfo) {
            throw 'The pinned CMake, Ninja, or dotnet environment changed.'
        }
    }
    finally { Restore-Environment $previous }
    $generationPrevious = Use-GenerationEnvironment $plan.Variants.baseline.InputVcpkgRoot
    try { Assert-ClangDriverSnapshot }
    finally { Restore-Environment $generationPrevious }
}

function Copy-NewFile {
    param([string] $Source, [string] $Destination)
    $stream = [IO.File]::Open($Destination, 'CreateNew', 'Write', 'None')
    try {
        $sourceStream = [IO.File]::Open($Source, 'Open', 'Read', 'Read')
        try { $sourceStream.CopyTo($stream) }
        finally { $sourceStream.Dispose() }
    }
    finally { $stream.Dispose() }
}

$resolvedPlan = Resolve-BenchmarkPhysicalPath $PlanPath
$plan = Read-Json $resolvedPlan
if ($plan.SchemaVersion -ne 1 -or $plan.ProductionAdoptionAuthorized -ne $false) {
    throw 'This adapter accepts only schema-1 experiment plans with no production authority.'
}
if ($plan.BaselineRevision -cne 'e94f10a9bb9490d47363cf43d8ce17600b435b8a' -or
    $plan.CandidateBehaviorRevision -cne '9952e5a76358028c22c8ec215a23d7b82413ad4f') {
    throw 'Benchmark code bindings do not match the approved comparison.'
}
if ($plan.HarnessBinding -ceq 'commit:PENDING-FINAL-HARNESS-COMMIT') {
    throw 'Formal workload execution is disabled until the final harness commit is bound.'
}
if ($plan.HarnessBinding -cne "commit:$($plan.CandidateHead)") {
    throw 'Harness binding and candidate HEAD differ.'
}
if ($plan.FixtureOnly) { throw 'Fixture-only OCCT plans cannot execute workloads.' }
if ($plan.NativeGate.FixtureOnly) { throw 'A fixture-only native boundary gate cannot execute workloads.' }
if ($plan.Variants.baseline.ArtifactRoot.Length -ne $plan.Variants.candidate.ArtifactRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot.Length -ne $plan.Variants.candidate.InputVcpkgRoot.Length -or
    $plan.Variants.baseline.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot -or
    $plan.Variants.candidate.InputVcpkgRoot -ceq $plan.ToolchainVcpkgRoot) {
    throw 'Frozen path-isolation or equal-length guarantees changed.'
}
if ($plan.Variants.baseline.OriginalHost.Length -ne $plan.Variants.candidate.OriginalHost.Length -or
    $plan.Variants.baseline.ChangedHost.Length -ne $plan.Variants.candidate.ChangedHost.Length) {
    throw 'Measured Console host path shape changed.'
}
Assert-IsolatedRoots
foreach ($path in @($resolvedPlan, $plan.Variants.candidate.RepositoryRoot,
        $plan.Variants.baseline.ArtifactRoot, $plan.Variants.candidate.ArtifactRoot,
        $plan.Variants.baseline.InputVcpkgRoot, $plan.Variants.candidate.InputVcpkgRoot,
        $plan.Variants.baseline.OriginalHostRoot, $plan.Variants.baseline.ChangedHostRoot,
        $plan.Variants.candidate.OriginalHostRoot, $plan.Variants.candidate.ChangedHostRoot)) {
    if ((Get-BenchmarkVolumeIdentity $path) -cne $plan.ArtifactVolumeIdentity) {
        throw 'A repository, specification/report, artifact, input, or host root moved away from the frozen physical volume.'
    }
}

$variantPlan = $plan.Variants[$Variant]
$sample = Resolve-BenchmarkPhysicalPath $SampleRoot
if (-not (Test-Path -LiteralPath $sample -PathType Container)) { throw 'SampleRoot must already exist.' }
Assert-RootMarker $variantPlan
$generatedRoot = Join-Path $variantPlan.ArtifactRoot 'generated'
$csharpRoot = Join-Path $generatedRoot 'csharp'
$cppRoot = Join-Path $generatedRoot 'cpp'
$nativeBuildRoot = Join-Path $variantPlan.ArtifactRoot 'native-build'
$unsupportedHeaders = Join-Path $generatedRoot 'unsupported-headers.txt'
$beforeManifest = Join-Path $sample 'artifacts-before.json'
$afterManifest = Join-Path $sample 'artifacts-after.json'
$beforeNinja = Join-Path $sample 'ninja-before.log'
$ninjaLog = Join-Path $nativeBuildRoot '.ninja_log'

switch ($Action) {
    'Prepare' {
        # Full binding verification is intentionally outside the measured Generate/Configure/Build actions.
        Assert-FrozenBindings
        Assert-RepositoryRevision $variantPlan
        Assert-FrozenInputs $variantPlan original
        Assert-NoNestedReparse $variantPlan.ArtifactRoot
        Assert-ToolchainSnapshot
        Assert-CanonicalBoundary
        Invoke-NativeGate $plan.Canonical.ArtifactRoot pre
        if ($Workload -eq 'artifact-cold') {
            $children = @(Get-ChildItem -LiteralPath $variantPlan.ArtifactRoot -Force |
                Where-Object Name -cne '.occt-benchmark-root.json')
            foreach ($child in $children) { Remove-Item -LiteralPath $child.FullName -Recurse -Force }
            $null = [IO.Directory]::CreateDirectory($generatedRoot)
            break
        }
        Assert-CompleteArtifacts
        Get-Manifest $beforeManifest $null
        Copy-NewFile $ninjaLog $beforeNinja
        if ($Workload -eq 'declaration-edit') {
            $inputManifest = Read-Json $plan.FrozenInputManifest
            Assert-MutablePrivateInputs $variantPlan $inputManifest.OriginalHeaderSha256
            [IO.File]::Copy($plan.ChangedHeaderFile, $variantPlan.HeaderPath, $true)
            Assert-FrozenInputs $variantPlan changed
        }
        elseif ($Workload -eq 'missing-output') {
            $missing = Assert-WithinRoot (Join-Path $generatedRoot $plan.MissingOutputRelativePath) $generatedRoot
            if (-not (Test-Path -LiteralPath $missing -PathType Leaf)) { throw 'Pinned missing-output target does not exist.' }
            Remove-Item -LiteralPath $missing
        }
    }
    'ValidateGenerationToolchain' {
        $changedHost = $Workload -eq 'generator-change'
        Invoke-GenerationToolchainValidation $variantPlan $changedHost
    }
    'Generate' {
        $changedHost = $Workload -eq 'generator-change'
        Invoke-Generation $variantPlan $changedHost
    }
    'Configure' {
        if ($Workload -ne 'artifact-cold') { throw 'cmake --fresh is allowed only for artifact-cold samples.' }
        $previous = Use-CompilerEnvironment
        try {
            Assert-ActiveCompilerEnvironment
            Invoke-Checked $plan.Tools.CMake @('--fresh', '-G', 'Ninja Multi-Config', '-Wno-unused-cli',
                '-S', $cppRoot, '-B', $nativeBuildRoot, "-DCMAKE_MAKE_PROGRAM=$($plan.Tools.Ninja)",
                "-DCMAKE_CXX_COMPILER=$($plan.Tools.Compiler)", "-DCMAKE_TOOLCHAIN_FILE=$($plan.ToolchainFile)",
                "-DVCPKG_TARGET_TRIPLET=$($plan.Triplet)", '-DVCPKG_APPLOCAL_DEPS=OFF')
        }
        finally { Restore-Environment $previous }
    }
    'Build' { Invoke-NativeBuild }
    'Verify' {
        Assert-FrozenBindings
        Assert-RepositoryRevision $variantPlan
        $headerState = if ($Workload -eq 'declaration-edit') { 'changed' } else { 'original' }
        Assert-FrozenInputs $variantPlan $headerState
        Assert-CompleteArtifacts
        $comparisonPath = if ($Workload -eq 'artifact-cold') { $null } else { $beforeManifest }
        Get-Manifest $afterManifest $comparisonPath
        $after = Read-Json $afterManifest
        $oracleManifest = if ($Workload -eq 'declaration-edit') {
            $plan.Canonical.DeclarationManifest
        }
        else { $plan.Canonical.OriginalManifest }
        Assert-ManifestOracle $after $oracleManifest
        if ($Workload -in @('unchanged', 'generator-change', 'missing-output') -and -not $after.Comparison.EqualContent) {
            throw "$Workload changed generated content unexpectedly."
        }
        if ($Workload -eq 'declaration-edit' -and $after.Comparison.EqualContent) {
            throw 'The representative declaration edit changed no generated content.'
        }
        if ($Variant -eq 'candidate' -and $Workload -in @('unchanged', 'generator-change') -and
            @($after.Comparison.ObservedRewritten).Count -ne 0) {
            throw 'The write-if-changed candidate rewrote unchanged sources.'
        }
        if ($Variant -eq 'baseline' -and $Workload -in @('unchanged', 'generator-change', 'missing-output') -and
            @($after.Comparison.ObservedRewritten).Count -eq 0) {
            throw 'The pinned baseline did not exhibit its expected rewrite behavior.'
        }
        if ($Workload -eq 'missing-output') {
            $missing = Assert-WithinRoot (Join-Path $generatedRoot $plan.MissingOutputRelativePath) $generatedRoot
            if (-not (Test-Path -LiteralPath $missing -PathType Leaf)) { throw 'Generation did not restore the missing output.' }
            if ($Variant -eq 'candidate') {
                $relative = $plan.MissingOutputRelativePath.Substring('cpp/'.Length)
                $expectedRewrite = '1/' + $relative
                $observed = @($after.Comparison.ObservedRewritten)
                if ($observed.Count -ne 1 -or $observed[0] -cne $expectedRewrite) {
                    throw 'The write-if-changed candidate rewrote more than the one restored output.'
                }
            }
        }
        $metricsArguments = @('-NoProfile', '-File', $plan.Tools.NinjaMetrics, '-LogPath', $ninjaLog,
            '-ReportPath', (Join-Path $sample 'native-metrics.json'))
        if ($Workload -ne 'artifact-cold') { $metricsArguments += @('-BeforeLogPath', $beforeNinja) }
        Invoke-Checked (Get-Process -Id $PID).Path $metricsArguments
        $oracleExports = if ($Workload -eq 'declaration-edit') {
            $plan.Canonical.DeclarationExportInventory
        }
        else { $plan.Canonical.OriginalExportInventory }
        $exportReport = Join-Path $sample 'exports-after.json'
        Invoke-Checked (Get-Process -Id $PID).Path @('-NoProfile', '-File', $plan.Tools.ExportInventory,
            '-SourcePath', (Join-Path $cppRoot 'NativeFunctionTable.cpp'), '-ReportPath', $exportReport,
            '-CompareTo', $oracleExports)
        if (-not (Read-Json $exportReport).Comparison.EqualOrderedExports) {
            throw 'Generated export order differs from the canonical baseline oracle.'
        }
        Invoke-NativeGate $variantPlan.ArtifactRoot post
        Write-NewJson (Join-Path $sample 'occt-verification.json') ([ordered]@{
            SchemaVersion = 1; Variant = $Variant; Workload = $Workload
            ArtifactManifest = $afterManifest; NinjaMetrics = (Join-Path $sample 'native-metrics.json')
            ExportInventory = $exportReport; NativeGate = 'pre-and-post-passed'
            HarnessBinding = $plan.HarnessBinding; ProductionAdoptionAuthorized = $false
        })
    }
    'Settle' {
        Assert-FrozenBindings
        if ($Workload -notin @('declaration-edit', 'generator-change')) {
            throw 'Only declaration-edit and generator-change samples require settlement.'
        }
        if ($Workload -eq 'declaration-edit') {
            $inputManifest = Read-Json $plan.FrozenInputManifest
            Assert-MutablePrivateInputs $variantPlan $inputManifest.ChangedHeaderSha256
            [IO.File]::Copy($plan.OriginalHeaderFile, $variantPlan.HeaderPath, $true)
        }
        Assert-FrozenInputs $variantPlan original
        Invoke-GenerationToolchainValidation $variantPlan $false
        Invoke-Generation $variantPlan $false
        Invoke-NativeBuild
        $settledPath = Join-Path $sample 'artifacts-settled.json'
        Get-Manifest $settledPath $beforeManifest
        $settled = Read-Json $settledPath
        if (-not $settled.Comparison.EqualContent) {
            throw 'Settlement did not restore the canonical generated content.'
        }
        Assert-ManifestOracle $settled $plan.Canonical.OriginalManifest
    }
}

Write-Output "$Action completed for $Workload/$Variant."
