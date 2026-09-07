#Requires -Version 7.5
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $GeneratorHost,

    [Parameter(Mandatory = $true)]
    [string] $GeneratedRoot,

    [Parameter(Mandatory = $true)]
    [string] $VcpkgRoot,

    [Parameter(Mandatory = $true)]
    [string] $Configuration
)

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

function Find-NativeToolchain {
    $visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
    $dumpbin = Get-ChildItem -LiteralPath $visualStudioRoot -Filter 'dumpbin.exe' -File -Recurse `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]Hostx64[\\/]x64[\\/]dumpbin\.exe$' } |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1
    if ($null -eq $dumpbin) {
        throw 'Visual Studio with the x64 C++ tools is required to resolve native dependencies.'
    }

    $redist = @{}
    Get-ChildItem -LiteralPath $visualStudioRoot -Filter '*.dll' -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]VC[\\/]Redist[\\/]MSVC[\\/].*[\\/]x64[\\/]' } |
        Sort-Object -Property FullName -Descending |
        ForEach-Object {
            if (-not $redist.ContainsKey($_.Name)) {
                $redist[$_.Name] = $_.FullName
            }
        }

    return [pscustomobject]@{
        Dumpbin = $dumpbin.FullName
        Redist = $redist
    }
}

function Stage-NativeDependencies {
    param(
        [Parameter(Mandatory = $true)]
        [string] $NativeLibrary,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $VcpkgBin,

        [Parameter(Mandatory = $true)]
        [object] $Toolchain
    )

    $resolvedDestination = [IO.Path]::GetFullPath($Destination)
    if (Test-Path -LiteralPath $resolvedDestination) {
        Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
    }

    $null = New-Item -ItemType Directory -Path $resolvedDestination
    $vcpkgFiles = @{}
    Get-ChildItem -LiteralPath $VcpkgBin -Filter '*.dll' -File | ForEach-Object {
        $vcpkgFiles[$_.Name] = $_.FullName
    }

    $sources = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($required in @('gmp-10.dll', 'mpfr-6.dll')) {
        if (-not $vcpkgFiles.ContainsKey($required)) {
            throw "The locked CGAL runtime dependency '$required' is absent from '$VcpkgBin'."
        }

        $sources[$required] = $vcpkgFiles[$required]
    }

    $queue = [Collections.Generic.Queue[string]]::new()
    $queue.Enqueue($NativeLibrary)
    foreach ($source in $sources.Values) {
        $queue.Enqueue($source)
    }

    $scanned = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $systemImports = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    while ($queue.Count -gt 0) {
        $binary = $queue.Dequeue()
        if (-not $scanned.Add([IO.Path]::GetFileName($binary))) {
            continue
        }

        foreach ($import in Get-ImportedDllNames -Dumpbin $Toolchain.Dumpbin -Binary $binary) {
            if ($sources.ContainsKey($import)) {
                continue
            }

            if ($vcpkgFiles.ContainsKey($import)) {
                $sources[$import] = $vcpkgFiles[$import]
                $queue.Enqueue($vcpkgFiles[$import])
                continue
            }

            if ($Toolchain.Redist.ContainsKey($import)) {
                $sources[$import] = $Toolchain.Redist[$import]
                $queue.Enqueue($Toolchain.Redist[$import])
                continue
            }

            $systemPath = Join-Path ([Environment]::SystemDirectory) $import
            if ((Test-Path -LiteralPath $systemPath -PathType Leaf) `
                -or $import -like 'api-ms-win-*.dll' `
                -or $import -like 'ext-ms-win-*.dll') {
                $null = $systemImports.Add($import)
                continue
            }

            throw "Native dependency '$import' imported by '$binary' could not be resolved."
        }
    }

    $dependencies = @($sources.GetEnumerator() | Sort-Object -Property Key | ForEach-Object {
        $destinationPath = Join-Path $resolvedDestination $_.Key
        Copy-Item -LiteralPath $_.Value -Destination $destinationPath
        [ordered]@{
            Name = $_.Key
            Source = $_.Value
            Hash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
        }
    })
    [ordered]@{
        Dependencies = $dependencies
        SystemImports = @($systemImports | Sort-Object)
    } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path (Split-Path $resolvedDestination -Parent) 'native-dependencies.json') `
            -Encoding utf8
}

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$generated = [IO.Path]::GetFullPath($GeneratedRoot)
$vcpkg = [IO.Path]::GetFullPath($VcpkgRoot)
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repository 'output'))
if (-not $generated.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "GeneratedRoot must remain under '$outputRoot'."
}

$mutexHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($generated)))
$mutex = [Threading.Mutex]::new($false, "Local\TedToolkit.CppBindings.Cgal.Windows.$mutexHash")
try {
    try {
        $mutex.WaitOne() | Out-Null
    }
    catch [Threading.AbandonedMutexException] {
        # The previous owner exited unexpectedly; this process now owns the mutex.
    }

    $stamp = Join-Path $generated "native-build\$Configuration\generation.stamp"
    $managedManifest = Join-Path $generated "native-build\$Configuration\managed-files.txt"
    $nativeLibrary = Join-Path $generated "native-build\$Configuration\ted_toolkit_cpp_bindings_cgal.dll"
    $dependencyManifest = Join-Path $generated 'native-dependencies.json'
    $inputRoots = @(
        (Join-Path $repository 'src\shared\TedToolkit.CppBindings.Generator'),
        (Join-Path $repository 'src\providers\cgal\TedToolkit.CppBindings.Cgal.Generator'),
        (Join-Path $repository 'src\providers\cgal\TedToolkit.CppBindings.Cgal.Generator.Tool'),
        (Join-Path $repository 'externals\TedToolkit\props')
    )
    $inputFiles = @($inputRoots |
        ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File } |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    $inputFiles += @(
        $PSCommandPath,
        (Join-Path $repository 'Directory.Build.props'),
        (Join-Path $repository 'Directory.Build.targets'),
        (Join-Path $repository 'Directory.Packages.props'),
        (Join-Path $repository 'src\providers\cgal\TedToolkit.CppBindings.Cgal.Windows\TedToolkit.CppBindings.Cgal.Windows.csproj'),
        (Join-Path $vcpkg 'installed\vcpkg\status'),
        (Join-Path $vcpkg 'installed\x64-windows\bin\gmp-10.dll'),
        (Join-Path $vcpkg 'installed\x64-windows\bin\mpfr-6.dll')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Get-Item
    $inputHashes = @($inputFiles | Sort-Object -Property FullName -Unique | ForEach-Object {
        "$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    })
    $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($inputHashes -join "`n"))))

    $managedComplete = $false
    if (Test-Path -LiteralPath $managedManifest) {
        $expectedManaged = @(Get-Content -LiteralPath $managedManifest | Where-Object { $_ })
        $actualManaged = @(Get-ChildItem -LiteralPath (Join-Path $generated 'csharp') -Filter '*.cs' -File `
            -ErrorAction SilentlyContinue)
        $managedComplete = $expectedManaged.Count -gt 0 `
            -and $expectedManaged.Count -eq $actualManaged.Count `
            -and @($expectedManaged | Where-Object {
                -not (Test-Path -LiteralPath (Join-Path $generated $_) -PathType Leaf)
            }).Count -eq 0
    }

    $dependenciesComplete = $false
    if (Test-Path -LiteralPath $dependencyManifest -PathType Leaf) {
        $dependencyState = Get-Content -LiteralPath $dependencyManifest -Raw | ConvertFrom-Json
        $dependenciesComplete = @($dependencyState.Dependencies).Count -gt 1 `
            -and @($dependencyState.Dependencies | Where-Object {
                $file = Join-Path $generated "native-dependencies\$($_.Name)"
                -not (Test-Path -LiteralPath $file -PathType Leaf) `
                    -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -cne $_.Hash
            }).Count -eq 0
    }

    if ((Test-Path -LiteralPath $stamp -PathType Leaf) `
        -and (Test-Path -LiteralPath $nativeLibrary -PathType Leaf) `
        -and (Get-Item -LiteralPath $nativeLibrary).Length -gt 0 `
        -and $managedComplete `
        -and $dependenciesComplete `
        -and (Get-Content -LiteralPath $stamp -Raw) -ceq $fingerprint) {
        Write-Output 'CGAL Windows bindings are up to date.'
        exit 0
    }

    if (Test-Path -LiteralPath $stamp) {
        Remove-Item -LiteralPath $stamp
    }

    $null = New-Item -ItemType Directory -Path $generated -Force
    & dotnet $GeneratorHost --output-root $generated --vcpkg-root $vcpkg
    if ($LASTEXITCODE -ne 0) {
        throw "The CGAL generator exited with code $LASTEXITCODE."
    }

    & cmake --fresh -G 'Visual Studio 18 2026' -A x64 `
        -S (Join-Path $generated 'cpp') -B (Join-Path $generated 'native-build') `
        "-DCMAKE_TOOLCHAIN_FILE=$(Join-Path $vcpkg 'scripts\buildsystems\vcpkg.cmake')" `
        -DVCPKG_TARGET_TRIPLET=x64-windows -DVCPKG_APPLOCAL_DEPS=OFF
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL native configuration failed with exit code $LASTEXITCODE."
    }

    & cmake --build (Join-Path $generated 'native-build') --config $Configuration --parallel 1
    if ($LASTEXITCODE -ne 0) {
        throw "CGAL native build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf) `
        -or (Get-Item -LiteralPath $nativeLibrary).Length -eq 0) {
        throw "The CGAL native library was not produced at '$nativeLibrary'."
    }

    $toolchain = Find-NativeToolchain
    Stage-NativeDependencies -NativeLibrary $nativeLibrary `
        -Destination (Join-Path $generated 'native-dependencies') `
        -VcpkgBin (Join-Path $vcpkg 'installed\x64-windows\bin') `
        -Toolchain $toolchain

    $managedFiles = @(Get-ChildItem -LiteralPath (Join-Path $generated 'csharp') -Filter '*.cs' -File)
    if ($managedFiles.Count -eq 0 -or @($managedFiles | Where-Object { $_.Length -eq 0 }).Count -ne 0) {
        throw 'The CGAL generator did not produce a complete managed source set.'
    }

    $null = New-Item -ItemType Directory -Path (Split-Path $managedManifest -Parent) -Force
    $managedFiles |
        ForEach-Object { [IO.Path]::GetRelativePath($generated, $_.FullName) } |
        Sort-Object |
        Set-Content -LiteralPath $managedManifest
    Set-Content -LiteralPath $stamp -Value $fingerprint -NoNewline
}
finally {
    try {
        $mutex.ReleaseMutex()
    }
    catch [ApplicationException] {
        # The mutex was not acquired, so there is nothing to release.
    }

    $mutex.Dispose()
}
