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
    $nativeByProvider = @{}
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
            $nativeByProvider[$provider] = $names
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
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="verified">
      <package pattern="TedToolkit.CppBindings.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.NETCore.App.Ref" />
      <package pattern="Microsoft.WindowsDesktop.App.Ref" />
      <package pattern="Microsoft.AspNetCore.App.Ref" />
    </packageSource>
  </packageSourceMapping>
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
        if ($Lane -eq 'win-x64') {
            dotnet run --project $project -c Release --no-restore --no-launch-profile -- $Lane
            continue
        }

        $output = Join-Path $consumer 'application'
        dotnet build $project -c Release --no-restore -o $output
        $trace = Join-Path $consumer 'loader.log'
        $loaderVariables = @(
            'LD_LIBRARY_PATH', 'LD_PRELOAD', 'LD_AUDIT', 'LD_DEBUG_OUTPUT',
            'DYLD_LIBRARY_PATH', 'DYLD_FALLBACK_LIBRARY_PATH', 'DYLD_VERSIONED_LIBRARY_PATH',
            'DYLD_FRAMEWORK_PATH', 'DYLD_FALLBACK_FRAMEWORK_PATH', 'DYLD_VERSIONED_FRAMEWORK_PATH',
            'DYLD_INSERT_LIBRARIES', 'DYLD_ROOT_PATH')
        $cleanEnvironment = @($loaderVariables | ForEach-Object { '-u'; $_ })
        $traceSetting, $loadPattern = if ($Lane -eq 'linux-x64') {
            'LD_DEBUG=libs', 'calling init:\s+(?<path>/\S.*)'
        }
        else {
            'DYLD_PRINT_LIBRARIES=1', '^dyld\[\d+\]:\s+(?:<[^>]+>\s+)?(?<path>/\S.*)'
        }
        try {
            env @cleanEnvironment $traceSetting dotnet (Join-Path $output 'Consumer.dll') $Lane *> $trace
        }
        catch {
            Get-Content -LiteralPath $trace -Tail 40
            throw
        }

        $allowedRoots = @(
            (realpath $output) + '/'
            (realpath $restoreCache) + '/')
        $expected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($provider in $selected) { $expected.UnionWith($nativeByProvider[$provider]) }
        $loaded = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($line in [IO.File]::ReadLines($trace)) {
            $match = [regex]::Match($line, $loadPattern)
            if (-not $match.Success) { continue }
            $path = $match.Groups['path'].Value.Trim()
            $name = [IO.Path]::GetFileName($path)
            if (-not $nativeFiles.ContainsKey($name)) { continue }
            if ($Lane -eq 'osx-arm64' -and ($path.StartsWith('/usr/lib/') -or $path.StartsWith('/System/Library/'))) {
                continue
            }
            $resolved = realpath $path
            if ($Lane -eq 'linux-x64' -and $resolved.StartsWith('/usr/lib/x86_64-linux-gnu/')) { continue }
            if (-not ($allowedRoots | Where-Object { $resolved.StartsWith($_, [StringComparison]::Ordinal) })) {
                throw "$scenario loaded $name outside the restored package or application directory: $resolved"
            }
            $null = $loaded.Add($name)
        }
        if (-not $expected.SetEquals($loaded)) {
            throw "$scenario loaded an unexpected set of packaged native files: $($loaded -join ', '). Expected: $($expected -join ', ')."
        }
        Write-Output "$scenario loaded $($loaded.Count) verified native files on $Lane."
    }
}
finally {
    $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
    if ($resolvedRoot -cne [IO.Path]::GetFullPath($root)) { throw "Unexpected consumer root: $resolvedRoot" }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
