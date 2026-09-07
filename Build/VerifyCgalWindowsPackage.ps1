#Requires -Version 7.5
param([string] $ReportDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-ImportedDllNames {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Dumpbin,

        [Parameter(Mandatory = $true)]
        [string] $Binary
    )

    $output = @(& $Dumpbin /NOLOGO /DEPENDENTS $Binary 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "dumpbin failed for '$Binary'."
    }

    $reading = $false
    $imports = [Collections.Generic.List[string]]::new()
    foreach ($line in $output) {
        $text = "$line".Trim()
        if ($text -ceq 'Image has the following dependencies:') {
            $reading = $true
            continue
        }

        if ($reading -and $text -ceq 'Summary') {
            break
        }

        if ($reading -and $text -match '^[A-Za-z0-9_.-]+\.dll$') {
            $imports.Add($text)
        }
    }

    return @($imports | Sort-Object -Unique)
}

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$candidateRevision = (& git -C $repository rev-parse HEAD).Trim()
$startingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($startingStatus.Count -ne 0) {
    throw 'CGAL Windows verification requires a clean exact candidate.'
}

if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repository ('out/verification/cw-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
}

$report = [IO.Path]::GetFullPath($ReportDirectory)
if (Test-Path -LiteralPath $report) {
    throw 'Use a new report directory; evidence is never overwritten.'
}

$null = New-Item -ItemType Directory -Path $report
$vcpkg = if ($env:VCPKG_ROOT) { [IO.Path]::GetFullPath($env:VCPKG_ROOT) } else { 'C:\vcpkg' }
$generated = Join-Path $repository 'output/providers/cgal'
$windowsProject = Join-Path $repository `
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj'
$buildLog = Join-Path $report 'windows-build.log'
& dotnet build $windowsProject -c Release --disable-build-servers --maxcpucount:1 `
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:VcpkgRoot=$vcpkg" *> $buildLog
if ($LASTEXITCODE -ne 0) {
    throw "CGAL Windows build failed; see $buildLog"
}

$feed = Join-Path $report 'feed'
$projects = [ordered]@{
    'TedToolkit.CppBindings.Runtime' =
        'src/shared/TedToolkit.CppBindings.Runtime/TedToolkit.CppBindings.Runtime.csproj'
    'TedToolkit.CppBindings.Cgal.Runtime' =
        'src/providers/cgal/TedToolkit.CppBindings.Cgal.Runtime/TedToolkit.CppBindings.Cgal.Runtime.csproj'
    'TedToolkit.CppBindings.Cgal.Windows' =
        'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/TedToolkit.CppBindings.Cgal.Windows.csproj'
}
foreach ($name in $projects.Keys) {
    $packLog = Join-Path $report "$name-pack.log"
    $arguments = @(
        'pack',
        (Join-Path $repository $projects[$name]),
        '-c',
        'Release',
        '-o',
        $feed,
        '--disable-build-servers',
        '--maxcpucount:1',
        '-p:GeneratePackageOnBuild=false',
        '-p:NuGetAudit=false',
        "-p:VcpkgRoot=$vcpkg")
    if ($name -ceq 'TedToolkit.CppBindings.Cgal.Windows') {
        $arguments += '--no-build'
    }

    & dotnet @arguments *> $packLog
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL packaging failed for '$name'; see $packLog"
    }
}

$package = Join-Path $feed 'TedToolkit.CppBindings.Cgal.Windows.1.0.0.nupkg'
$dependencyManifest = Get-Content -LiteralPath (Join-Path $generated 'native-dependencies.json') `
    -Raw | ConvertFrom-Json
$generationResult = Get-Content -LiteralPath (Join-Path $generated 'generation-result.json') `
    -Raw | ConvertFrom-Json
$managedInventory = @(Get-Content -LiteralPath (Join-Path $generated 'csharp/managed-inventory.json') `
    -Raw | ConvertFrom-Json)
$nativeInventory = @(Get-Content -LiteralPath (Join-Path $generated 'cpp/native-inventory.json') `
    -Raw | ConvertFrom-Json)
if ($generationResult.ProfileId -cne 'epick-windows-v1' `
    -or $generationResult.AdmittedCount -ne 19 `
    -or $generationResult.NativeExportCount -ne 17 `
    -or $managedInventory.Count -ne 19 `
    -or $nativeInventory.Count -ne 19) {
    throw 'The generated package inputs do not match the verified finite profile.'
}

$managedIds = @($managedInventory.DeclarationId | Sort-Object)
$nativeIds = @($nativeInventory.DeclarationId | Sort-Object)
if (($managedIds -join "`n") -cne ($nativeIds -join "`n")) {
    throw 'Managed and native declaration inventories do not identify the same admitted surface.'
}

$functionTable = Get-Content -LiteralPath (Join-Path $generated 'cpp/NativeFunctionTable.cpp') -Raw
$nativeSymbols = @([regex]::Matches($functionTable, 'extern "C" void ([A-Za-z0-9_]+)\(\);') |
    ForEach-Object { $_.Groups[1].Value })
if ($nativeSymbols.Count -ne 17 -or @($nativeSymbols | Sort-Object -Unique).Count -ne 17) {
    throw 'The generated native function table does not contain exactly 17 unique exports.'
}

$extractRoot = Join-Path $report 'package-content'
[IO.Compression.ZipFile]::ExtractToDirectory($package, $extractRoot)
$managedEntry = Join-Path $extractRoot 'lib/net8.0/TedToolkit.CppBindings.Cgal.Windows.dll'
$nativeRoot = Join-Path $extractRoot 'runtimes/win-x64/native'
$packedNative = Join-Path $nativeRoot 'ted_toolkit_cpp_bindings_cgal.dll'
if (-not (Test-Path -LiteralPath $managedEntry -PathType Leaf) `
    -or -not (Test-Path -LiteralPath $packedNative -PathType Leaf)) {
    throw 'The CGAL Windows package is missing its generated managed or native binding.'
}

$expectedDependencies = @($dependencyManifest.Dependencies.Name | Sort-Object)
$actualNativeFiles = @(Get-ChildItem -LiteralPath $nativeRoot -Filter '*.dll' -File | Select-Object -ExpandProperty Name)
$expectedNativeFiles = @('ted_toolkit_cpp_bindings_cgal.dll') + $expectedDependencies | Sort-Object
if (($actualNativeFiles | Sort-Object) -join "`n" -cne ($expectedNativeFiles -join "`n")) {
    throw 'The packed native DLL set differs from the recursively staged dependency set.'
}

foreach ($dependency in $dependencyManifest.Dependencies) {
    $packedDependency = Join-Path $nativeRoot $dependency.Name
    if ((Get-FileHash -LiteralPath $packedDependency -Algorithm SHA256).Hash -cne $dependency.Hash) {
        throw "Packed dependency '$($dependency.Name)' differs from the scanned input."
    }
}

$builtNative = Join-Path $generated 'native-build/Release/ted_toolkit_cpp_bindings_cgal.dll'
$nativeHash = (Get-FileHash -LiteralPath $builtNative -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $packedNative -Algorithm SHA256).Hash -cne $nativeHash) {
    throw 'The packed CGAL native library differs from the compiled library.'
}

$builtManaged = Join-Path $repository `
    'src/providers/cgal/TedToolkit.CppBindings.Cgal.Windows/bin/Release/net8.0/TedToolkit.CppBindings.Cgal.Windows.dll'
$managedHash = (Get-FileHash -LiteralPath $builtManaged -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $managedEntry -Algorithm SHA256).Hash -cne $managedHash) {
    throw 'The packed CGAL managed assembly differs from the compiled assembly.'
}

foreach ($notice in @('CGAL.txt', 'GMP.txt', 'MPFR.txt')) {
    $noticePath = Join-Path $extractRoot "third-party-notices/$notice"
    if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf) `
        -or (Get-Item -LiteralPath $noticePath).Length -eq 0) {
        throw "The CGAL Windows package is missing notice '$notice'."
    }
}

$visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
$dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) {
    throw 'Visual Studio dumpbin is required to verify package dependency closure.'
}

$packagedByName = @{}
Get-ChildItem -LiteralPath $nativeRoot -Filter '*.dll' -File | ForEach-Object {
    $packagedByName[$_.Name] = $_.FullName
}
foreach ($binary in $packagedByName.Values) {
    foreach ($import in Get-ImportedDllNames -Dumpbin $dumpbin -Binary $binary) {
        if ($packagedByName.ContainsKey($import)) {
            continue
        }

        $systemPath = Join-Path ([Environment]::SystemDirectory) $import
        if ((Test-Path -LiteralPath $systemPath -PathType Leaf) `
            -or $import -like 'api-ms-win-*.dll' `
            -or $import -like 'ext-ms-win-*.dll') {
            continue
        }

        throw "Package dependency closure is missing '$import', imported by '$binary'."
    }
}
foreach ($required in @('gmp-10.dll', 'mpfr-6.dll')) {
    if (-not $packagedByName.ContainsKey($required)) {
        throw "The locked runtime dependency '$required' is absent from the package."
    }
}

$exports = @(& $dumpbin /NOLOGO /EXPORTS $packedNative 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw 'dumpbin could not inspect the packed CGAL native exports.'
}
$exportText = $exports -join "`n"
foreach ($symbol in $nativeSymbols + 'NativeApi_GetFunctionTable') {
    if ($exportText -notmatch "(?m)\b$([regex]::Escape($symbol))\b") {
        throw "The packed CGAL native library is missing export '$symbol'."
    }
}

$consumer = Join-Path $report 'consumer'
$null = New-Item -ItemType Directory -Path $consumer
$fixture = Join-Path $repository 'tests/TedToolkit.CppBindings.Cgal.Windows.Tests/Fixtures/PackageConsumer'
Get-ChildItem -LiteralPath $fixture -File | Copy-Item -Destination $consumer
$consumerResultPath = Join-Path $report 'consumer-result.json'
$consumerLog = Join-Path $report 'consumer-run.log'
$packages = Join-Path $repository 'out/package-cache/cgal-windows'
$null = New-Item -ItemType Directory -Path $packages -Force
foreach ($packageId in @(
    'tedtoolkit.cppbindings.runtime',
    'tedtoolkit.cppbindings.cgal.runtime',
    'tedtoolkit.cppbindings.cgal.windows')) {
    $cachedPackage = Join-Path $packages $packageId
    if (Test-Path -LiteralPath $cachedPackage) {
        Remove-Item -LiteralPath $cachedPackage -Recurse -Force
    }
}

& dotnet run --project (Join-Path $consumer 'PackageConsumer.csproj') -c Release `
    --disable-build-servers --no-launch-profile `
    "-p:RestoreSources=$feed" `
    -p:RestoreAdditionalProjectSources=https://api.nuget.org/v3/index.json `
    "-p:RestorePackagesPath=$packages" -p:NuGetAudit=false -- $consumerResultPath *> $consumerLog
if ($LASTEXITCODE -ne 0) {
    throw "The isolated CGAL Windows consumer failed; see $consumerLog"
}

$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') `
    -Raw | ConvertFrom-Json -AsHashtable
foreach ($name in $projects.Keys) {
    $packagePath = Join-Path $feed "$name.1.0.0.nupkg"
    $expectedHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($packagePath)))
    if ($assets.libraries["$name/1.0.0"].sha512 -cne $expectedHash) {
        throw "The consumer did not restore the just-built '$name' package."
    }
}
if (@($assets.libraries.Keys | Where-Object { $_ -match 'Generator|Clang|Occt' }).Count -ne 0) {
    throw 'The CGAL Windows consumer acquired generation tooling or OCCT.'
}

$consumerResult = Get-Content -LiteralPath $consumerResultPath -Raw | ConvertFrom-Json
if ($consumerResult.SquaredDistance2 -ne 25 `
    -or $consumerResult.SquaredDistance3 -ne 9 `
    -or $consumerResult.IntersectionKind -cne 'Point' `
    -or $consumerResult.IntersectionX -ne 1 `
    -or $consumerResult.IntersectionY -ne 0 `
    -or $consumerResult.EmptyIntersectionKind -cne 'None' `
    -or $consumerResult.PreconditionType -cne 'CGAL::Precondition_exception' `
    -or [string]::IsNullOrWhiteSpace($consumerResult.PreconditionMessage) `
    -or [string]::IsNullOrWhiteSpace($consumerResult.PreconditionStack)) {
    throw 'The real CGAL package consumer did not observe every required value and diagnostic.'
}

$endingRevision = (& git -C $repository rev-parse HEAD).Trim()
$endingStatus = @(& git -C $repository status --porcelain --untracked-files=all)
if ($endingRevision -cne $candidateRevision -or $endingStatus.Count -ne 0) {
    throw 'The candidate revision or worktree changed during CGAL Windows verification.'
}

[ordered]@{
    Passed = $true
    CandidateRevision = $candidateRevision
    Profile = $generationResult.ProfileId
    AdmittedCount = $generationResult.AdmittedCount
    ManagedArtifactCount = $managedInventory.Count
    NativeArtifactCount = $nativeInventory.Count
    NativeExportCount = $nativeSymbols.Count
    NativeDependencies = $expectedDependencies
    NativeDependencyCount = $expectedDependencies.Count
    NoticeCount = 3
    Toolchain = $generationResult.Toolchain
    ManagedAssemblyHash = $managedHash
    NativeLibraryHash = $nativeHash
    WindowsPackageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    Consumer = $consumerResult
} | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8
Write-Output "CGAL Windows package verification passed: $report"
