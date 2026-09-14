#Requires -Version 7.5

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-PackageImportedDllNames {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Dumpbin,

        [Parameter(Mandatory = $true)]
        [string] $Binary
    )

    $output = @(& $Dumpbin /NOLOGO /DEPENDENTS $Binary 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "dumpbin failed for packaged binary '$Binary'."
    }

    $parseError = "dumpbin dependency output was not recognized for packaged binary '$Binary'."
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
        throw "dumpbin header inspection failed for packaged binary '$Binary'."
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

function Assert-ExactPackageNativeClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $NativeRoot,

        [Parameter(Mandatory = $true)]
        [string] $BindingName,

        [Parameter(Mandatory = $true)]
        [string] $Dumpbin
    )

    $binding = Join-Path $NativeRoot $BindingName
    if (-not (Test-Path -LiteralPath $binding -PathType Leaf)) {
        throw "Packaged binding '$BindingName' is absent."
    }

    $files = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem -LiteralPath $NativeRoot -Filter '*.dll' -File | ForEach-Object {
        $files[$_.Name] = $_.FullName
    }

    $reachable = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $queue = [Collections.Generic.Queue[string]]::new()
    $queue.Enqueue($binding)
    while ($queue.Count -gt 0) {
        $binary = $queue.Dequeue()
        $name = [IO.Path]::GetFileName($binary)
        if (-not $reachable.Add($name)) {
            continue
        }

        foreach ($import in Get-PackageImportedDllNames -Dumpbin $Dumpbin -Binary $binary) {
            if ($files.ContainsKey($import)) {
                $queue.Enqueue($files[$import])
                continue
            }

            $systemPath = Join-Path ([Environment]::SystemDirectory) $import
            if ((Test-Path -LiteralPath $systemPath -PathType Leaf) `
                -or $import -like 'api-ms-win-*.dll' `
                -or $import -like 'ext-ms-win-*.dll') {
                continue
            }

            throw "Package dependency closure is missing '$import', imported by '$name'."
        }
    }

    $packagedNames = @($files.Keys | Sort-Object)
    $reachableNames = @($reachable | Sort-Object)
    if (($packagedNames -join "`n") -cne ($reachableNames -join "`n")) {
        $extra = @($packagedNames | Where-Object { -not $reachable.Contains($_) })
        throw "Package contains DLLs outside the '$BindingName' import closure: $($extra -join ', ')."
    }

    return @($reachableNames | ForEach-Object {
        [ordered]@{
            Name = $_
            Hash = (Get-FileHash -LiteralPath $files[$_] -Algorithm SHA256).Hash
        }
    })
}

Export-ModuleMember -Function Assert-ExactPackageNativeClosure
