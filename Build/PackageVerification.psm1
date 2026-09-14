Set-StrictMode -Version Latest

function Get-NuGetPackageArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Feed,

        [Parameter(Mandatory = $true)]
        [string] $PackageId
    )

    $packages = @(Get-ChildItem -LiteralPath $Feed -Filter "$PackageId.*.nupkg" -File)
    if ($packages.Count -ne 1) {
        throw "Expected one '$PackageId' package in '$Feed', but found $($packages.Count)."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
    try {
        $nuspecs = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspecs.Count -ne 1) {
            throw "Expected one nuspec in '$($packages[0].FullName)', but found $($nuspecs.Count)."
        }

        $stream = $nuspecs[0].Open()
        $reader = [IO.StreamReader]::new($stream)
        try {
            [xml] $nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }

        $metadata = $nuspec.SelectSingleNode(
            "/*[local-name()='package']/*[local-name()='metadata']")
        $actualId = $metadata.SelectSingleNode("*[local-name()='id']").InnerText
        $version = $metadata.SelectSingleNode("*[local-name()='version']").InnerText
        if ($actualId -cne $PackageId -or [string]::IsNullOrWhiteSpace($version)) {
            throw "Package '$($packages[0].FullName)' has unexpected identity '$actualId/$version'."
        }

        return [pscustomobject]@{
            Id = $actualId
            Version = $version
            Path = $packages[0].FullName
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-UniformNuGetPackageArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Feed,

        [Parameter(Mandatory = $true)]
        [string[]] $PackageIds
    )

    $paths = [ordered]@{}
    $version = $null
    foreach ($packageId in $PackageIds) {
        $artifact = Get-NuGetPackageArtifact -Feed $Feed -PackageId $packageId
        if ($null -eq $version) {
            $version = $artifact.Version
        }
        elseif ($version -cne $artifact.Version) {
            throw "Package '$packageId' has version '$($artifact.Version)', expected '$version'."
        }

        $paths[$packageId] = $artifact.Path
    }

    return [pscustomobject]@{
        Version = $version
        Paths = $paths
    }
}

Export-ModuleMember -Function Get-NuGetPackageArtifact, Get-UniformNuGetPackageArtifacts
