$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'tests\TedToolkit.CppBindings.Windows.Generation.Tool.Tests\TedToolkit.CppBindings.Windows.Generation.Tool.Tests.csproj'

dotnet run --project $project --configuration Release -p:GeneratePackageOnBuild=false -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    throw "Windows generation cache verification failed with exit code $LASTEXITCODE."
}

Write-Output 'Windows generation cache proof passed for OCCT, CGAL, Manifold, and FCL.'
