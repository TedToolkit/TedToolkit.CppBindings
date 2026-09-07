[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$sourcePath = [System.IO.Path]::GetFullPath($Source)
$outputPath = [System.IO.Path]::GetFullPath($Output)
$outputDirectory = Split-Path -Parent $outputPath
$sourceDirectory = Split-Path -Parent $sourcePath
$buildDirectory = Join-Path $PSScriptRoot 'obj/NativeFixture'

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& cmake --fresh -G 'Visual Studio 18 2026' -A x64 -S $sourceDirectory -B $buildDirectory `
    "-DCMAKE_RUNTIME_OUTPUT_DIRECTORY=$outputDirectory" -DVCPKG_APPLOCAL_DEPS=OFF
if ($LASTEXITCODE -ne 0) {
    throw "Native fixture configuration failed with exit code $LASTEXITCODE."
}

& cmake --build $buildDirectory --config Release --parallel 1
if ($LASTEXITCODE -ne 0) {
    throw "Native fixture build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Native fixture was not produced at '$outputPath'."
}
