#Requires -Version 7.5

param(
    [string] $VcpkgRoot = $env:VCPKG_ROOT,
    [string] $ReportDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if ([string]::IsNullOrWhiteSpace($VcpkgRoot)) {
    $VcpkgRoot = 'C:\vcpkg'
}
$VcpkgRoot = [IO.Path]::GetFullPath($VcpkgRoot)
if (-not (Test-Path -LiteralPath $VcpkgRoot -PathType Container)) {
    throw "The vcpkg root '$VcpkgRoot' does not exist."
}

if ([string]::IsNullOrWhiteSpace($ReportDirectory)) {
    $ReportDirectory = Join-Path $repository (
        'out/verification/native-library-version/' + (Get-Date -Format 'yyyyMMdd-HHmmss-ffff'))
}
$report = New-Item -ItemType Directory -Path ([IO.Path]::GetFullPath($ReportDirectory)) -Force
$toolProject = Join-Path $repository (
    'src/tools/TedToolkit.CppBindings.Windows.Generation.Tool/' +
    'TedToolkit.CppBindings.Windows.Generation.Tool.csproj')
$buildLog = Join-Path $report 'tool-build.log'
& dotnet build $toolProject -c Release --no-restore *> $buildLog
if ($LASTEXITCODE -ne 0) {
    throw "The Windows generation tool did not build; see '$buildLog'."
}

$tool = Join-Path (Split-Path $toolProject -Parent) (
    'bin/Release/net10.0/TedToolkit.CppBindings.Windows.Generation.Tool.dll')
$profiles = @(
    [pscustomobject]@{
        Id = 'occt'
        Package = 'OpenCASCADE'
        Version = '8.0.1'
        NativeLibrary = 'ted_toolkit_occt.dll'
    },
    [pscustomobject]@{
        Id = 'cgal'
        Package = 'CGAL'
        Version = '6.2'
        NativeLibrary = 'ted_toolkit_cpp_bindings_cgal.dll'
    },
    [pscustomobject]@{
        Id = 'manifold'
        Package = 'manifold'
        Version = '3.5.2'
        NativeLibrary = 'ted_toolkit_cpp_bindings_manifold.dll'
    },
    [pscustomobject]@{
        Id = 'fcl'
        Package = 'fcl'
        Version = '0.7.0'
        NativeLibrary = 'ted_toolkit_cpp_bindings_fcl.dll'
    }
)

$results = foreach ($profile in $profiles) {
    $output = Join-Path $report $profile.Id
    $log = Join-Path $report "$($profile.Id).log"
    & dotnet $tool `
        --provider $profile.Id `
        --repository-root $repository `
        --output-root $output `
        --vcpkg-root $VcpkgRoot `
        --configuration Release *> $log
    if ($LASTEXITCODE -ne 0) {
        throw "Exact-version generation failed for $($profile.Id); see '$log'."
    }

    $cmake = Join-Path $output 'cpp/CMakeLists.txt'
    $expected = "find_package($($profile.Package) $($profile.Version) EXACT CONFIG REQUIRED)"
    if (-not (Test-Path -LiteralPath $cmake -PathType Leaf)) {
        throw "The $($profile.Id) generated CMake project was not produced."
    }
    $matchingLines = @(Get-Content -LiteralPath $cmake | Where-Object { $_ -ceq $expected })
    if ($matchingLines.Count -ne 1) {
        throw "The $($profile.Id) CMake project does not contain exactly '$expected'."
    }

    $nativeLibrary = Join-Path $output ("native-build/Release/" + $profile.NativeLibrary)
    if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf)) {
        throw "The exact-version native library '$nativeLibrary' was not built."
    }

    [ordered]@{
        Provider = $profile.Id
        Package = $profile.Package
        Version = $profile.Version
        Requirement = $expected
        CMakeHash = (Get-FileHash -LiteralPath $cmake -Algorithm SHA256).Hash
        NativeLibraryHash = (Get-FileHash -LiteralPath $nativeLibrary -Algorithm SHA256).Hash
        Log = $log
    }
}

[ordered]@{
    Passed = $true
    VcpkgRoot = $VcpkgRoot
    Results = $results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $report 'result.json') -Encoding utf8

Write-Output "Exact native library version verification passed: $report"
