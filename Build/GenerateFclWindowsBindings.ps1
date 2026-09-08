#Requires -Version 7.5
param(
    [Parameter(Mandatory = $true)] [string] $RepositoryRoot,
    [Parameter(Mandatory = $true)] [string] $GeneratorHost,
    [Parameter(Mandatory = $true)] [string] $GeneratedRoot,
    [Parameter(Mandatory = $true)] [string] $VcpkgRoot,
    [Parameter(Mandatory = $true)] [string] $Configuration
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'NativeDependencyClosure.psm1') -Force

function Get-FclOutputFiles {
    param([string] $Root, [string] $Configuration)
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($directory in @('csharp', 'cpp', 'native-dependencies', 'third-party-notices')) {
        $path = Join-Path $Root $directory
        if (Test-Path -LiteralPath $path -PathType Container) {
            $files.AddRange([IO.FileInfo[]]@(Get-ChildItem -LiteralPath $path -Recurse -File))
        }
    }
    foreach ($relativePath in @(
            'generation-result.json', 'build-toolchain.json', 'native-dependencies.json',
            "native-build/$Configuration/ted_toolkit_cpp_bindings_fcl.dll")) {
        $path = Join-Path $Root $relativePath
        if (Test-Path -LiteralPath $path -PathType Leaf) { $files.Add((Get-Item -LiteralPath $path)) }
    }
    return @($files | Sort-Object -Property FullName -Unique)
}

function Write-FclOutputManifest {
    param([string] $Root, [string] $Configuration, [string] $Manifest)
    $entries = @(Get-FclOutputFiles -Root $Root -Configuration $Configuration | ForEach-Object {
        [ordered]@{
            Path = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
            Length = $_.Length
            Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
    if ($entries.Count -eq 0) { throw 'Cannot record an empty FCL output manifest.' }
    [ordered]@{ Files = $entries } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Manifest -Encoding utf8
}

function Test-FclOutputManifest {
    param([string] $Root, [string] $Configuration, [string] $Manifest)
    if (-not (Test-Path -LiteralPath $Manifest -PathType Leaf)) { return $false }
    try {
        $recorded = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json | Select-Object -ExpandProperty Files)
        $actual = @(Get-FclOutputFiles -Root $Root -Configuration $Configuration)
        if ($recorded.Count -eq 0 -or $recorded.Count -ne $actual.Count) { return $false }
        $actualByPath = @{}
        foreach ($file in $actual) {
            $actualByPath[[IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')] = $file
        }
        foreach ($entry in $recorded) {
            if (-not $actualByPath.ContainsKey($entry.Path)) { return $false }
            $file = $actualByPath[$entry.Path]
            if ($file.Length -ne $entry.Length -or
                (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne $entry.Hash) {
                return $false
            }
        }
        return $true
    }
    catch { return $false }
}

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$generated = [IO.Path]::GetFullPath($GeneratedRoot)
$vcpkg = [IO.Path]::GetFullPath($VcpkgRoot)
$allowedRoots = @('output', 'out') | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $repository $_)) }
if (-not @($allowedRoots | Where-Object {
        $generated.StartsWith($_ + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    }).Count) {
    throw 'GeneratedRoot must remain under the repository output or evidence root.'
}

$mutexHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($generated)))
$mutex = [Threading.Mutex]::new($false, "Local\TedToolkit.CppBindings.Fcl.Windows.$mutexHash")
try {
    try { $mutex.WaitOne() | Out-Null } catch [Threading.AbandonedMutexException] { }
    $nativeLibrary = Join-Path $generated "native-build\$Configuration\ted_toolkit_cpp_bindings_fcl.dll"
    $stamp = Join-Path $generated "native-build\$Configuration\generation.stamp"
    $dependencyManifest = Join-Path $generated 'native-dependencies.json'
    $outputManifest = Join-Path $generated 'output-manifest.json'
    $inputs = @(
        (Get-ChildItem -LiteralPath (Join-Path $repository 'src/providers/fcl') -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }),
        (Get-Item -LiteralPath $PSCommandPath),
        (Get-Item -LiteralPath (Join-Path $repository 'Build/NativeDependencyClosure.psm1')),
        (Get-Item -LiteralPath (Join-Path $vcpkg 'installed/vcpkg/status'))
    ) | ForEach-Object { $_ } | Sort-Object -Property FullName -Unique
    $fingerprintLines = @($inputs | ForEach-Object {
        "$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    })
    $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($fingerprintLines -join "`n"))))
    $dependenciesComplete = Test-NativeDependencyClosure -NativeLibrary $nativeLibrary `
        -Destination (Join-Path $generated 'native-dependencies') -Manifest $dependencyManifest
    $noticesComplete = @('FCL.txt', 'libccd.txt', 'Eigen.txt', 'Octomap.txt').Where({
        $notice = Join-Path $generated "third-party-notices/$_"
        -not (Test-Path -LiteralPath $notice -PathType Leaf) -or (Get-Item -LiteralPath $notice).Length -eq 0
    }).Count -eq 0
    $outputsComplete = Test-FclOutputManifest -Root $generated -Configuration $Configuration -Manifest $outputManifest
    if ((Test-Path -LiteralPath $stamp -PathType Leaf) -and
        (Get-Content -LiteralPath $stamp -Raw) -ceq $fingerprint -and
        (Test-Path -LiteralPath (Join-Path $generated 'csharp/Fcl.Bindings.g.cs') -PathType Leaf) -and
        $dependenciesComplete -and $noticesComplete -and $outputsComplete) {
        Write-Output 'FCL Windows bindings are up to date.'
        exit 0
    }

    if (Test-Path -LiteralPath $stamp) { Remove-Item -LiteralPath $stamp }
    $null = New-Item -ItemType Directory -Path $generated -Force
    $scratchRoot = $env:TEDTOOLKIT_NATIVE_SCRATCH_ROOT
    Assert-NativeBuildDiskBoundary -Path $generated -Phase 'FCL generation' -ScratchRoot $scratchRoot | Out-Null
    & dotnet $GeneratorHost --output-root $generated
    if ($LASTEXITCODE -ne 0) { throw "The FCL generator exited with code $LASTEXITCODE." }

    & cmake --fresh -G 'Visual Studio 18 2026' -A x64 `
        -S (Join-Path $generated 'cpp') -B (Join-Path $generated 'native-build') `
        "-DCMAKE_TOOLCHAIN_FILE=$(Join-Path $vcpkg 'scripts/buildsystems/vcpkg.cmake')" `
        -DVCPKG_TARGET_TRIPLET=x64-windows -DVCPKG_APPLOCAL_DEPS=OFF
    if ($LASTEXITCODE -ne 0) { throw "FCL native configuration failed with exit code $LASTEXITCODE." }

    Assert-NativeBuildDiskBoundary -Path $generated -Phase 'FCL native compilation' -ScratchRoot $scratchRoot | Out-Null
    & cmake --build (Join-Path $generated 'native-build') --config $Configuration --parallel 1
    if ($LASTEXITCODE -ne 0) { throw "FCL native build failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf)) {
        throw "The FCL native library was not produced at '$nativeLibrary'."
    }

    $profile = Get-Content -LiteralPath (Join-Path $generated 'csharp/profile-manifest.json') -Raw | ConvertFrom-Json
    $compilerParts = (Get-Content -LiteralPath (Join-Path $generated 'native-build/compiler-identity.txt') -Raw).
        Trim().Split('|', 3)
    if ($compilerParts.Count -ne 3 -or $compilerParts[0] -cne 'MSVC' -or
        $compilerParts[1] -cne "$($profile.Versions.Msvc).0") {
        throw "The selected compiler '$($compilerParts -join '|')' differs from the locked profile."
    }
    $cmakeVersionLine = (& cmake --version | Select-Object -First 1)
    if ($cmakeVersionLine -notmatch '^cmake version (.+)$' -or $Matches[1] -cne $profile.Versions.CMake) {
        throw "The selected CMake '$cmakeVersionLine' differs from the locked profile."
    }
    $toolchain = Get-WindowsNativeToolchain -Compiler $compilerParts[2]
    [ordered]@{
        CompilerId = $compilerParts[0]
        CompilerVersion = $compilerParts[1]
        CompilerPath = $compilerParts[2]
        CMake = $Matches[1]
        ToolsetVersion = $toolchain.ToolsetVersion
    } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $generated 'build-toolchain.json') -Encoding utf8
    $null = Set-NativeDependencyClosure -NativeLibrary $nativeLibrary `
        -Destination (Join-Path $generated 'native-dependencies') `
        -VcpkgBin (Join-Path $vcpkg 'installed/x64-windows/bin') `
        -Toolchain $toolchain -OwnedRoot $generated

    $notices = Join-Path $generated 'third-party-notices'
    if (Test-Path -LiteralPath $notices) { Remove-Item -LiteralPath $notices -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $notices
    ([ordered]@{
        'FCL.txt' = Join-Path $vcpkg 'installed/x64-windows/share/fcl/copyright'
        'libccd.txt' = Join-Path $vcpkg 'installed/x64-windows/share/ccd/copyright'
        'Eigen.txt' = Join-Path $vcpkg 'installed/x64-windows/share/eigen3/copyright'
        'Octomap.txt' = Join-Path $vcpkg 'installed/x64-windows/share/octomap/copyright'
    }).GetEnumerator() | ForEach-Object {
        if (-not (Test-Path -LiteralPath $_.Value -PathType Leaf)) { throw "Required notice input '$($_.Value)' is missing." }
        Copy-Item -LiteralPath $_.Value -Destination (Join-Path $notices $_.Key)
    }

    Write-FclOutputManifest -Root $generated -Configuration $Configuration -Manifest $outputManifest
    $null = New-Item -ItemType Directory -Path (Split-Path $stamp -Parent) -Force
    Set-Content -LiteralPath $stamp -Value $fingerprint -NoNewline
}
finally {
    try { $mutex.ReleaseMutex() } catch [ApplicationException] { }
    $mutex.Dispose()
}
