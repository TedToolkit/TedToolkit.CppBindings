param(
    [Parameter(Mandatory)] [ValidateSet('win-x64', 'linux-x64', 'osx-arm64')] [string] $Lane,
    [Parameter(Mandatory)] [string] $PackageDirectory,
    [string] $SharedPackageDirectory = $PackageDirectory,
    [Parameter(Mandatory)] [string] $Version
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-StrictMode -Version Latest

$hostRid = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
if ($hostRid -cne $Lane) { throw "Expected a native $Lane consumer host, got $hostRid." }
$platform = @{ 'win-x64' = 'Windows'; 'linux-x64' = 'Linux'; 'osx-arm64' = 'MacOS' }[$Lane]
$packageRoot = (Resolve-Path -LiteralPath $PackageDirectory).Path
$sharedRoot = (Resolve-Path -LiteralPath $SharedPackageDirectory).Path
$providers = @('Occt', 'Cgal', 'Manifold', 'Fcl', 'Smlib')
$fixture = Join-Path (Split-Path $PSScriptRoot -Parent) 'tests/TedToolkit.CppBindings.NativePackageIsolation.Tests/Fixtures/PlatformConsumer/Program.cs'
$root = Join-Path ([IO.Path]::GetTempPath()) ('cppbindings-consumers-' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $root 'feed'
$null = New-Item -ItemType Directory -Path $feed

$packages = @{}
foreach ($provider in $providers) {
    $id = "TedToolkit.CppBindings.$provider.$platform"
    $packages[$id] = Join-Path $packageRoot "$id.$Version.nupkg"
}
foreach ($name in @('Runtime', 'Occt.Runtime', 'Cgal.Runtime', 'Manifold.Runtime', 'Fcl.Runtime')) {
    $id = "TedToolkit.CppBindings.$name"
    $packages[$id] = Join-Path $sharedRoot "$id.$Version.nupkg"
}

try {
    $nativeFiles = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    $nativePrefix = "runtimes/$Lane/native/"
    foreach ($provider in $providers) {
        $id = "TedToolkit.CppBindings.$provider.$platform"
        $archive = [IO.Compression.ZipFile]::OpenRead($packages[$id])
        try {
            $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($entry in $archive.Entries) {
                if (-not $entry.FullName.StartsWith($nativePrefix, [StringComparison]::Ordinal) -or -not $entry.Name) {
                    continue
                }
                if ($entry.FullName -cne "$nativePrefix$($entry.Name)" -or -not $names.Add($entry.Name)) {
                    throw "$id has an invalid or duplicate native entry: $($entry.FullName)."
                }
                $stream = $entry.Open()
                try {
                    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
                }
                finally {
                    $stream.Dispose()
                }
                if ($nativeFiles.ContainsKey($entry.Name) -and $nativeFiles[$entry.Name] -cne $hash) {
                    throw "Conflicting native file $($entry.Name) in $id."
                }
                $nativeFiles[$entry.Name] = $hash
            }
            if ($names.Count -eq 0) { throw "$id contains no native assets for $Lane." }
        }
        finally {
            $archive.Dispose()
        }
    }

    foreach ($path in $packages.Values) {
        Copy-Item -LiteralPath $path -Destination $feed
    }
    $config = Join-Path $root 'NuGet.Config'
    @'
<configuration>
  <packageSources>
    <clear />
    <add key="verified" value="feed" />
  </packageSources>
</configuration>
'@ | Set-Content -LiteralPath $config -Encoding utf8

    foreach ($scenario in ($providers + 'All')) {
        $selected = if ($scenario -eq 'All') { $providers } else { @($scenario) }
        $constants = ($selected | ForEach-Object { $_.ToUpperInvariant() }) -join ';'
        $references = ($selected | ForEach-Object {
            "    <PackageReference Include=`"TedToolkit.CppBindings.$_.$platform`" Version=`"[$Version]`" />"
        }) -join "`n"
        $consumer = Join-Path $root $scenario
        $null = New-Item -ItemType Directory -Path $consumer
        Copy-Item -LiteralPath $fixture -Destination $consumer
        $project = Join-Path $consumer 'Consumer.csproj'
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>$Lane</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <UseAppHost>false</UseAppHost>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <DefineConstants>$constants</DefineConstants>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
$references
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        $restoreCache = Join-Path $consumer 'restored'
        dotnet restore $project --configfile $config --packages $restoreCache --no-http-cache
        $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
        foreach ($dependency in $assets.libraries.GetEnumerator()) {
            $id, $restoredVersion = $dependency.Key.Split('/', 2)
            if ($restoredVersion -cne $Version -or -not $packages.ContainsKey($id)) {
                throw "Unexpected dependency for ${scenario}: $($dependency.Key)"
            }
            $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($packages[$id])))
            if ($dependency.Value.sha512 -cne $hash) {
                throw "$($dependency.Key) did not restore from the verified package."
            }
        }
        dotnet run --project $project -c Release --no-restore --no-launch-profile -- $Lane
    }
}
finally {
    $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
    if ($resolvedRoot -cne [IO.Path]::GetFullPath($root)) { throw "Unexpected consumer root: $resolvedRoot" }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
