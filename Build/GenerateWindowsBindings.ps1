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
$resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$resolvedGeneratedRoot = [IO.Path]::GetFullPath($GeneratedRoot)
$mutexHash = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($resolvedGeneratedRoot)))
$mutex = [Threading.Mutex]::new($false, "Local\TedToolkit.CppBindings.Occt.Windows.$mutexHash")
$previousEnvironment = @{}

try {
    try {
        $mutex.WaitOne() | Out-Null
    }
    catch [Threading.AbandonedMutexException] {
        # The previous owner exited unexpectedly; this process now owns the mutex.
    }

    $stampPath = Join-Path $resolvedGeneratedRoot "native-build\$Configuration\generation.stamp"
    $manifestPath = Join-Path $resolvedGeneratedRoot "native-build\$Configuration\managed-files.txt"
    $nativeLibraryPath = Join-Path $resolvedGeneratedRoot "native-build\$Configuration\ted_toolkit_occt.dll"
    $generatorInputRoots = @(
        (Join-Path $resolvedRepositoryRoot 'src\core\TedToolkit.CppBindings.Generator'),
        (Join-Path $resolvedRepositoryRoot 'src\core\TedToolkit.CppBindings.Occt.Generator'),
        (Join-Path $resolvedRepositoryRoot 'tests\TedToolkit.CppBindings.Occt.Console'),
        (Join-Path $resolvedRepositoryRoot 'src\tools\TedToolkit.CppBindings.Occt.SourceGenerators'),
        (Join-Path $resolvedRepositoryRoot 'externals\TedToolkit\TedToolkit.RoslynHelper'),
        (Join-Path $resolvedRepositoryRoot 'externals\TedToolkit\props')
    )
    $inputFiles = @($generatorInputRoots |
        Where-Object { Test-Path -LiteralPath $_ } |
        ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File } |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    $inputFiles += @(
        $PSCommandPath,
        (Join-Path $resolvedRepositoryRoot 'Directory.Build.props'),
        (Join-Path $resolvedRepositoryRoot 'Directory.Build.targets'),
        (Join-Path $resolvedRepositoryRoot 'Directory.Packages.props'),
        (Join-Path $resolvedRepositoryRoot 'src\core\TedToolkit.CppBindings.Occt.Windows\TedToolkit.CppBindings.Occt.Windows.csproj'),
        (Join-Path $VcpkgRoot 'installed\vcpkg\status')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Get-Item
    $inputHashes = @($inputFiles | Sort-Object -Property FullName -Unique | ForEach-Object {
        "$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    })
    $inputHashes += [IO.Path]::GetFullPath($VcpkgRoot)
    $inputFingerprint = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($inputHashes -join "`n"))))

    $managedFilesComplete = $false
    if (Test-Path -LiteralPath $manifestPath) {
        $managedFiles = @(Get-Content -LiteralPath $manifestPath | Where-Object { $_ })
        $actualManagedFiles = @(Get-ChildItem -LiteralPath (Join-Path $resolvedGeneratedRoot 'csharp') -File -Filter '*.cs' -ErrorAction SilentlyContinue)
        $managedFilesComplete = $managedFiles.Count -gt 0 -and
            $actualManagedFiles.Count -eq $managedFiles.Count -and
            @($actualManagedFiles | Where-Object { $_.Length -eq 0 }).Count -eq 0 -and
            @($managedFiles | Where-Object {
                -not (Test-Path -LiteralPath (Join-Path $resolvedGeneratedRoot $_) -PathType Leaf)
            }).Count -eq 0
    }

    if ((Test-Path -LiteralPath $stampPath) -and
        (Test-Path -LiteralPath $nativeLibraryPath) -and
        $managedFilesComplete -and
        ((Get-Item -LiteralPath $nativeLibraryPath).Length -gt 0) -and
        ((Get-Content -LiteralPath $stampPath -Raw) -eq $inputFingerprint)) {
        Write-Output 'Windows bindings are up to date.'
        exit 0
    }

    if (Test-Path -LiteralPath $stampPath) {
        Remove-Item -LiteralPath $stampPath
    }

    & dotnet $GeneratorHost --output-root $resolvedGeneratedRoot
    if ($LASTEXITCODE -ne 0) {
        throw "The OCCT generator exited with code $LASTEXITCODE."
    }

    $visualStudioRoot = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Microsoft Visual Studio'
    $toolchain = Get-ChildItem -LiteralPath $visualStudioRoot -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue } |
        Sort-Object -Property FullName -Descending |
        ForEach-Object {
            $ninja = Join-Path $_.FullName 'Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe'
            $environment = Join-Path $_.FullName 'VC\Auxiliary\Build\vcvars64.bat'
            $compiler = Get-ChildItem -LiteralPath (Join-Path $_.FullName 'VC\Tools\MSVC') -Directory -ErrorAction SilentlyContinue |
                Sort-Object -Property Name -Descending |
                ForEach-Object { Join-Path $_.FullName 'bin\Hostx64\x64\cl.exe' } |
                Where-Object { Test-Path -LiteralPath $_ } |
                Select-Object -First 1
            if ((Test-Path -LiteralPath $ninja) -and (Test-Path -LiteralPath $environment) -and $compiler) {
                [pscustomobject]@{ Ninja = $ninja; Compiler = $compiler; Environment = $environment }
            }
        } |
        Select-Object -First 1
    if ($null -eq $toolchain) {
        throw 'Visual Studio with the MSVC C++ and CMake components is required to generate Windows bindings.'
    }

    $environmentLines = & $env:COMSPEC /d /c "call `"$($toolchain.Environment)`" >nul && set"
    if ($LASTEXITCODE -ne 0) {
        throw 'The Visual Studio x64 compiler environment could not be initialized.'
    }

    $compilerEnvironment = @{}
    foreach ($line in $environmentLines) {
        if ($line -match '^([^=]+)=(.*)$') {
            $compilerEnvironment[$Matches[1]] = $Matches[2]
        }
    }

    foreach ($entry in $compilerEnvironment.GetEnumerator()) {
        $previousEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $configureOutput = @(& cmake --fresh -G 'Ninja Multi-Config' -Wno-unused-cli -S "$resolvedGeneratedRoot\cpp" -B "$resolvedGeneratedRoot\native-build" "-DCMAKE_MAKE_PROGRAM=$($toolchain.Ninja)" "-DCMAKE_CXX_COMPILER=$($toolchain.Compiler)" "-DCMAKE_TOOLCHAIN_FILE=$VcpkgRoot\scripts\buildsystems\vcpkg.cmake" -DVCPKG_TARGET_TRIPLET=x64-windows -DVCPKG_APPLOCAL_DEPS=OFF 2>&1)
    $configureExit = $LASTEXITCODE
    $configureOutput | Write-Output
    if ($configureExit -ne 0) {
        throw "CMake configuration exited with code $configureExit."
    }
    if (($configureOutput -join "`n") -match 'cannot be safely placed') {
        throw 'The native object paths exceed the compiler budget. Use a shorter GeneratedRoot before compiling.'
    }

    & cmake --build "$resolvedGeneratedRoot\native-build" --config $Configuration --parallel 8
    if ($LASTEXITCODE -ne 0) {
        throw "The native build exited with code $LASTEXITCODE."
    }

    $managedFiles = @(Get-ChildItem -LiteralPath (Join-Path $resolvedGeneratedRoot 'csharp') -File -Filter '*.cs')
    if ($managedFiles.Count -eq 0 -or @($managedFiles | Where-Object { $_.Length -eq 0 }).Count -gt 0) {
        throw 'The OCCT generator did not produce a complete nonempty managed source set.'
    }

    if (-not (Test-Path -LiteralPath $nativeLibraryPath) -or (Get-Item -LiteralPath $nativeLibraryPath).Length -eq 0) {
        throw 'The native build did not produce a nonempty binding library.'
    }

    $managedFiles |
        ForEach-Object { [IO.Path]::GetRelativePath($resolvedGeneratedRoot, $_.FullName) } |
        Sort-Object |
        Set-Content -LiteralPath $manifestPath
    Set-Content -LiteralPath $stampPath -Value $inputFingerprint -NoNewline
}
finally {
    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        $originalValue = if ($null -eq $entry.Value) { [NullString]::Value } else { $entry.Value }
        [Environment]::SetEnvironmentVariable($entry.Key, $originalValue, 'Process')
    }

    try {
        $mutex.ReleaseMutex()
    }
    catch [ApplicationException] {
        # The mutex was not acquired, so there is nothing to release.
    }

    $mutex.Dispose()
}
