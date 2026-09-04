$ErrorActionPreference = 'Stop'
$coordinator = Join-Path $PSScriptRoot 'GenerateWindowsBindings.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('occt-generation-proof-' + [Guid]::NewGuid().ToString('N'))
$generatedRoot = Join-Path $testRoot 'output\generated'
$state = @{ Generations = 0; FailNative = $false; WarnNativePaths = $false; NativeBuilds = 0 }
$originalComSpec = $env:COMSPEC
$originalPath = $env:PATH
$probeName = 'OCCT_ENV_PROOF_' + [Guid]::NewGuid().ToString('N')
$state.SyntheticEnvironment = $true

function Invoke-TestCompilerEnvironment {
    "PATH=$originalPath;first"
    "Path=$originalPath;last"
    "${probeName}=first"
    "$($probeName.ToLowerInvariant())=last"
    $global:LASTEXITCODE = 0
}

function Assert-GenerationCount([int] $expected, [string] $scenario) {
    if ($state.Generations -ne $expected) {
        throw "$scenario expected $expected generations, got $($state.Generations)."
    }
}

function dotnet {
    $state.Generations++
    New-Item -ItemType Directory -Path (Join-Path $generatedRoot 'csharp') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $generatedRoot 'cpp') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $generatedRoot 'csharp\Type.g.cs') -Value 'struct Type {}'
    $global:LASTEXITCODE = 0
}

function cmake {
    if ($args -contains '--build') { $state.NativeBuilds++ }
    if ($state.WarnNativePaths) {
        'CMake Warning: object file cannot be safely placed under this directory.'
        $global:LASTEXITCODE = 0
        return
    }
    if ($state.SyntheticEnvironment -and
        ($env:PATH -cne "$originalPath;last" -or
            [Environment]::GetEnvironmentVariable($probeName, 'Process') -cne 'last')) {
        throw 'Compiler environment import must use the last value for case-insensitive duplicate names.'
    }

    if ($state.FailNative) {
        $global:LASTEXITCODE = 1
        return
    }

    New-Item -ItemType Directory -Path (Join-Path $generatedRoot 'native-build\Release') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $generatedRoot 'native-build\Release\ted_toolkit_occt.dll') -Value 'fixture'
    $global:LASTEXITCODE = 0
}

function Assert-EnvironmentRestored {
    if ($env:PATH -cne $originalPath -or
        $null -ne [Environment]::GetEnvironmentVariable($probeName, 'Process')) {
        throw "Compiler environment restoration failed: PATH restored=$($env:PATH -ceq $originalPath); probe absent=$($null -eq [Environment]::GetEnvironmentVariable($probeName, 'Process'))."
    }
}

function Invoke-Coordinator {
    try {
        & $coordinator -RepositoryRoot $testRoot -GeneratorHost 'test-host.dll' -GeneratedRoot $generatedRoot `
            -VcpkgRoot $testRoot -Configuration Release
    }
    finally {
        Assert-EnvironmentRestored
    }
}

try {
    $env:COMSPEC = 'Invoke-TestCompilerEnvironment'
    $sourceRoot = Join-Path $testRoot 'src\core\TedToolkit.CppBindings.Generator'
    New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
    $source = Join-Path $sourceRoot 'Input.cs'
    Set-Content -LiteralPath $source -Value 'first input'
    Invoke-Coordinator
    Assert-GenerationCount 1 'Cold generation'
    Invoke-Coordinator
    Assert-GenerationCount 1 'Unchanged generation'

    Remove-Item -LiteralPath (Join-Path $generatedRoot 'csharp\Type.g.cs')
    Invoke-Coordinator
    Assert-GenerationCount 2 'Missing managed output'
    Clear-Content -LiteralPath (Join-Path $generatedRoot 'csharp\Type.g.cs')
    Invoke-Coordinator
    Assert-GenerationCount 3 'Empty managed output'
    Remove-Item -LiteralPath (Join-Path $generatedRoot 'native-build\Release\ted_toolkit_occt.dll')
    Invoke-Coordinator
    Assert-GenerationCount 4 'Missing native output'

    Set-Content -LiteralPath $source -Value 'changed input'
    Invoke-Coordinator
    Assert-GenerationCount 5 'Changed input'
    Remove-Item -LiteralPath $source
    Invoke-Coordinator
    Assert-GenerationCount 6 'Deleted input'

    $stamp = Join-Path $generatedRoot 'native-build\Release\generation.stamp'
    Set-Content -LiteralPath $stamp -Value 'invalid stamp'
    $state.FailNative = $true
    $failed = $false
    try { Invoke-Coordinator } catch { $failed = $true }
    Assert-EnvironmentRestored
    if (-not $failed -or (Test-Path -LiteralPath $stamp)) {
        throw 'A failed native build must fail closed without publishing a successful stamp.'
    }

    $state.FailNative = $false
    Invoke-Coordinator
    Assert-GenerationCount 8 'Retry after failed native build'
    Invoke-Coordinator
    Assert-GenerationCount 8 'Unchanged generation after recovery'

    Remove-Item -LiteralPath (Join-Path $generatedRoot 'csharp\Type.g.cs')
    $state.FailNative = $true
    $failed = $false
    try { Invoke-Coordinator } catch { $failed = $true }
    Assert-EnvironmentRestored
    if (-not $failed -or (Test-Path -LiteralPath $stamp)) {
        throw 'Output recovery failure must invalidate the previously successful stamp.'
    }

    $state.FailNative = $false
    Invoke-Coordinator
    Assert-GenerationCount 10 'Retry after output recovery failure'
    Invoke-Coordinator
    Assert-GenerationCount 10 'Unchanged generation after output recovery'

    $env:COMSPEC = $originalComSpec
    $state.SyntheticEnvironment = $false
    Remove-Item -LiteralPath $stamp
    Invoke-Coordinator
    Assert-GenerationCount 11 'Real compiler environment restoration'
    Remove-Item -LiteralPath $stamp
    $state.WarnNativePaths = $true
    $nativeBuilds = $state.NativeBuilds
    $failed = $false
    try { Invoke-Coordinator } catch { $failed = $_.Exception.Message -like '*compiler budget*' }
    if (-not $failed -or $state.NativeBuilds -ne $nativeBuilds -or (Test-Path -LiteralPath $stamp)) {
        throw 'Unsafe object paths must fail during configuration, before native compilation or stamp publication.'
    }
    Write-Output 'Generation cache proof passed: cold, unchanged, missing/empty managed, missing native, changed/deleted input, failed build, failed output recovery, retry and duplicate-case/real compiler environment restoration.'
}
finally {
    $env:COMSPEC = $originalComSpec
    $env:PATH = $originalPath
    [Environment]::SetEnvironmentVariable($probeName, [NullString]::Value, 'Process')
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedTestRoot.StartsWith($resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedTestRoot) -notlike 'occt-generation-proof-*') {
        throw 'Refusing cleanup outside the exact temporary proof directory.'
    }

    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
