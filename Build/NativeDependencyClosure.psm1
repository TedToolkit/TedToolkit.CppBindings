#Requires -Version 7.5

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-NativeBuildDiskBoundary {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Phase,

        [string] $ScratchRoot,

        [double] $FreeSpaceFloorGiB = 25,

        [double] $ScratchBudgetGiB = 12
    )

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($resolvedPath))
    $freeBytes = $drive.AvailableFreeSpace
    $scratchBytes = 0L
    if ($ScratchRoot) {
        $resolvedScratch = [IO.Path]::GetFullPath($ScratchRoot)
        if (Test-Path -LiteralPath $resolvedScratch -PathType Container) {
            $scratchFiles = @(Get-ChildItem -LiteralPath $resolvedScratch -Recurse -File `
                -ErrorAction SilentlyContinue)
            if ($scratchFiles.Count -ne 0) {
                $scratchBytes = [long](($scratchFiles | Measure-Object -Property Length -Sum).Sum)
            }
        }
    }

    $freeFloorBytes = [long]($FreeSpaceFloorGiB * 1GB)
    $scratchBudgetBytes = [long]($ScratchBudgetGiB * 1GB)
    if ($freeBytes -lt $freeFloorBytes -or $scratchBytes -gt $scratchBudgetBytes) {
        throw ("Disk guard stopped phase '{0}': free={1:N2} GiB (floor={2:N2} GiB), " +
            "scratch={3:N2} GiB (budget={4:N2} GiB).") -f $Phase, ($freeBytes / 1GB),
            $FreeSpaceFloorGiB, ($scratchBytes / 1GB), $ScratchBudgetGiB
    }

    return [pscustomobject]@{
        Phase = $Phase
        FreeBytes = $freeBytes
        ScratchBytes = $scratchBytes
    }
}

function Get-WindowsImportedDllNames {
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

    $parseError = "dumpbin dependency output was not recognized for '$Binary'."
    $regularSeen = $false
    $delaySeen = $false
    $summarySeen = $false
    $currentSection = $null
    $regularImports = [Collections.Generic.List[string]]::new()
    $delayImports = [Collections.Generic.List[string]]::new()
    foreach ($line in $output) {
        $text = "$line".Trim()
        if ($text -ceq 'Image has the following dependencies:') {
            if ($regularSeen) {
                throw $parseError
            }
            $regularSeen = $true
            $currentSection = 'Regular'
            continue
        }

        if ($text -ceq 'Image has the following delay load dependencies:') {
            if ($delaySeen) {
                throw $parseError
            }
            $delaySeen = $true
            $currentSection = 'Delay'
            continue
        }

        if ($text -ceq 'Summary') {
            $summarySeen = $true
            break
        }

        if ($null -ne $currentSection -and $text.Length -ne 0) {
            $isDllName = $text.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase) `
                -and [IO.Path]::GetFileName($text) -ceq $text `
                -and $text.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -lt 0
            if (-not $isDllName) {
                throw $parseError
            }

            if ($currentSection -ceq 'Regular') {
                $regularImports.Add($text)
            }
            else {
                $delayImports.Add($text)
            }
        }
    }

    if (-not $summarySeen -or ($regularSeen -and $regularImports.Count -eq 0) `
        -or ($delaySeen -and $delayImports.Count -eq 0)) {
        throw $parseError
    }

    $headerOutput = @(& $Dumpbin /NOLOGO /HEADERS $Binary 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "dumpbin header inspection failed for '$Binary'."
    }
    $directories = @{}
    foreach ($line in $headerOutput) {
        if ("$line" -match '^\s*([0-9A-Fa-f]+)\s+\[\s*([0-9A-Fa-f]+)\]\s+RVA \[size\] of ((?:Delay )?Import) Directory\s*$') {
            $kind = if ($Matches[3] -ceq 'Delay Import') { 'Delay' } else { 'Regular' }
            if ($directories.ContainsKey($kind)) {
                throw $parseError
            }
            $rva = [Convert]::ToUInt64($Matches[1], 16)
            $size = [Convert]::ToUInt64($Matches[2], 16)
            if (($rva -eq 0) -xor ($size -eq 0)) {
                throw $parseError
            }
            $directories[$kind] = $rva -ne 0
        }
    }

    if ($directories.Count -ne 2 -or -not $directories.ContainsKey('Regular') `
        -or -not $directories.ContainsKey('Delay') `
        -or [bool]$directories['Regular'] -ne $regularSeen `
        -or [bool]$directories['Delay'] -ne $delaySeen) {
        throw $parseError
    }

    $imports = @($regularImports) + @($delayImports)
    return @($imports | Sort-Object -Unique)
}

function Get-WindowsNativeToolchain {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Compiler
    )

    $compilerPath = [IO.Path]::GetFullPath($Compiler)
    if ($compilerPath -notmatch '^(.*[\\/]VC)[\\/]Tools[\\/]MSVC[\\/]([^\\/]+)[\\/]bin[\\/]Hostx64[\\/]x64[\\/]cl\.exe$') {
        throw "CMake selected an unsupported compiler path '$compilerPath'."
    }

    $vcRoot = $Matches[1]
    $toolsetVersion = $Matches[2]
    $dumpbin = Join-Path (Split-Path $compilerPath -Parent) 'dumpbin.exe'
    if (-not (Test-Path -LiteralPath $dumpbin -PathType Leaf)) {
        throw "The selected compiler toolset has no matching dumpbin at '$dumpbin'."
    }

    $redistRoot = Join-Path $vcRoot "Redist\MSVC\$toolsetVersion\x64"
    if (-not (Test-Path -LiteralPath $redistRoot -PathType Container)) {
        throw "The selected compiler toolset has no matching x64 redistributable directory."
    }

    $redist = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $redistRoot -Filter '*.dll' -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object -Property FullName -Descending |
        ForEach-Object {
            if (-not $redist.ContainsKey($_.Name)) {
                $redist[$_.Name] = $_.FullName
            }
        }

    return [pscustomobject]@{
        Dumpbin = $dumpbin
        Redist = $redist
        ToolsetVersion = $toolsetVersion
    }
}

function Test-NativeDependencyClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $NativeLibrary,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $Manifest
    )

    try {
        if (-not (Test-Path -LiteralPath $NativeLibrary -PathType Leaf) `
            -or -not (Test-Path -LiteralPath $Manifest -PathType Leaf) `
            -or -not (Test-Path -LiteralPath $Destination -PathType Container)) {
            return $false
        }

        $state = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
        if ($state.Root.Name -cne [IO.Path]::GetFileName($NativeLibrary) `
            -or $state.Root.Hash -cne (Get-FileHash -LiteralPath $NativeLibrary -Algorithm SHA256).Hash) {
            return $false
        }

        $recorded = @($state.Dependencies)
        $actual = @(Get-ChildItem -LiteralPath $Destination -Filter '*.dll' -File)
        if ($recorded.Count -ne $actual.Count) {
            return $false
        }

        $actualNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($file in $actual) {
            $null = $actualNames.Add($file.Name)
        }

        foreach ($dependency in $recorded) {
            $staged = Join-Path $Destination $dependency.Name
            if (-not $actualNames.Contains($dependency.Name) `
                -or -not (Test-Path -LiteralPath $dependency.Source -PathType Leaf) `
                -or (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -cne $dependency.Hash `
                -or (Get-FileHash -LiteralPath $dependency.Source -Algorithm SHA256).Hash -cne $dependency.Hash) {
                return $false
            }
        }

        $fingerprintLines = @(
            "root|$($state.Root.Name)|$($state.Root.Hash)"
            $recorded | Sort-Object -Property Name | ForEach-Object { "dependency|$($_.Name)|$($_.Hash)" }
            @($state.SystemImports) | Sort-Object | ForEach-Object { "system|$_" }
        )
        $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes(($fingerprintLines -join "`n"))))
        return $fingerprint -ceq $state.Fingerprint
    }
    catch {
        return $false
    }
}

function Set-NativeDependencyClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $NativeLibrary,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $VcpkgBin,

        [Parameter(Mandatory = $true)]
        [object] $Toolchain,

        [Parameter(Mandatory = $true)]
        [string] $OwnedRoot
    )

    $root = [IO.Path]::GetFullPath($OwnedRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $native = [IO.Path]::GetFullPath($NativeLibrary)
    $destinationPath = [IO.Path]::GetFullPath($Destination)
    if (-not $destinationPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Native dependency destination must remain beneath '$root'."
    }

    if (-not (Test-Path -LiteralPath $native -PathType Leaf)) {
        throw "Native binding library is absent: '$native'."
    }

    if (-not (Test-Path -LiteralPath $VcpkgBin -PathType Container)) {
        throw "The vcpkg runtime directory is absent: '$VcpkgBin'."
    }

    $manifestPath = Join-Path (Split-Path $destinationPath -Parent) 'native-dependencies.json'
    $vcpkgFiles = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $VcpkgBin -Filter '*.dll' -File | ForEach-Object {
        $vcpkgFiles[$_.Name] = $_.FullName
    }

    $sources = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $queue = [Collections.Generic.Queue[string]]::new()
    $queue.Enqueue($native)
    $scanned = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $systemImports = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    while ($queue.Count -gt 0) {
        $binary = $queue.Dequeue()
        if (-not $scanned.Add([IO.Path]::GetFileName($binary))) {
            continue
        }

        foreach ($import in Get-WindowsImportedDllNames -Dumpbin $Toolchain.Dumpbin -Binary $binary) {
            if ($sources.ContainsKey($import)) {
                continue
            }

            $source = if ($vcpkgFiles.ContainsKey($import)) {
                $vcpkgFiles[$import]
            }
            elseif ($Toolchain.Redist.ContainsKey($import)) {
                $Toolchain.Redist[$import]
            }
            else {
                $null
            }
            if ($source) {
                $sources[$import] = $source
                $queue.Enqueue($source)
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

    $previousNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        try {
            (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).Dependencies.Name |
                ForEach-Object { $null = $previousNames.Add($_) }
        }
        catch {
            throw "Existing native dependency manifest is invalid: '$manifestPath'."
        }
    }

    if (Test-Path -LiteralPath $destinationPath -PathType Container) {
        $unmanaged = @(Get-ChildItem -LiteralPath $destinationPath -File | Where-Object {
            -not $previousNames.Contains($_.Name)
        })
        if ($unmanaged.Count -ne 0) {
            throw "Native dependency destination contains unmanaged files: $($unmanaged.Name -join ', ')."
        }
    }

    $staging = Join-Path (Split-Path $destinationPath -Parent) `
        ('.native-dependencies-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $staging
    try {
        $dependencies = @($sources.GetEnumerator() | Sort-Object -Property Key | ForEach-Object {
            $staged = Join-Path $staging $_.Key
            Copy-Item -LiteralPath $_.Value -Destination $staged
            [ordered]@{
                Name = $_.Key
                Source = [IO.Path]::GetFullPath($_.Value)
                Hash = (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash
            }
        })
        $rootHash = (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash
        $fingerprintLines = @(
            "root|$([IO.Path]::GetFileName($native))|$rootHash"
            $dependencies | ForEach-Object { "dependency|$($_.Name)|$($_.Hash)" }
            @($systemImports) | Sort-Object | ForEach-Object { "system|$_" }
        )
        $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes(($fingerprintLines -join "`n"))))
        $state = [ordered]@{
            Root = [ordered]@{ Name = [IO.Path]::GetFileName($native); Hash = $rootHash }
            Dependencies = $dependencies
            SystemImports = @($systemImports | Sort-Object)
            Fingerprint = $fingerprint
        }
        $temporaryManifest = "$manifestPath.tmp"
        $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporaryManifest -Encoding utf8

        $null = New-Item -ItemType Directory -Path $destinationPath -Force
        foreach ($file in @(Get-ChildItem -LiteralPath $destinationPath -File)) {
            if ($previousNames.Contains($file.Name)) {
                Remove-Item -LiteralPath $file.FullName
            }
        }
        Get-ChildItem -LiteralPath $staging -File | Move-Item -Destination $destinationPath
        Move-Item -LiteralPath $temporaryManifest -Destination $manifestPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }

    if (-not (Test-NativeDependencyClosure -NativeLibrary $native -Destination $destinationPath `
            -Manifest $manifestPath)) {
        throw 'The staged native dependency closure failed its integrity check.'
    }

    return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

Export-ModuleMember -Function Assert-NativeBuildDiskBoundary, Get-WindowsImportedDllNames, `
    Get-WindowsNativeToolchain, Set-NativeDependencyClosure, Test-NativeDependencyClosure
