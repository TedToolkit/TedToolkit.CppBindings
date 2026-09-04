param(
    [Parameter(Mandatory)] [string[]] $Roots,
    [Parameter(Mandatory)] [string] $ReportPath,
    [string[]] $Files = @(),
    [string] $CompareTo
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$report = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $report) { throw 'Refusing to overwrite an artifact manifest.' }
$resolvedRoots = @($Roots | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
$resolvedFiles = @($Files | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
$artifactEntries = [Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
$index = 0
foreach ($root in $resolvedRoots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "Not an artifact directory: $root" }
    $prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($report.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Store manifests outside the measured artifact roots.'
    }
    if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Artifact roots must not be reparse points.'
    }
    $entries = @(Get-ChildItem -LiteralPath $root -Recurse -Force)
    if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
        throw 'Artifact manifests do not follow symbolic links or junctions.'
    }
    foreach ($file in $entries | Where-Object { -not $_.PSIsContainer }) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        $key = "$index/$relative"
        # Refuse concurrent writers while hashing. Manifest collection is outside timed stages.
        $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
            $artifactEntries.Add($key, [ordered]@{
                Path = $key
                Bytes = $stream.Length
                Sha256 = $digest
                LastWriteTimeUtcTicks = [IO.File]::GetLastWriteTimeUtc($file.FullName).Ticks
            })
        }
        finally { $stream.Dispose() }
    }
    $index++
}
foreach ($path in $resolvedFiles) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Not an artifact file: $path" }
    if ($path -ceq $report) { throw 'The report cannot also be an artifact input.' }
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Artifact manifests do not follow symbolic links or junctions.'
    }
    foreach ($root in $resolvedRoots) {
        $prefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if ($path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Additional artifact files must be outside the measured artifact roots.'
        }
    }
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $key = "$index/$([IO.Path]::GetFileName($path))"
        $digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
        $artifactEntries.Add($key, [ordered]@{
            Path = $key
            Bytes = $stream.Length
            Sha256 = $digest
            LastWriteTimeUtcTicks = [IO.File]::GetLastWriteTimeUtc($path).Ticks
        })
    }
    finally { $stream.Dispose() }
    $index++
}

$comparison = $null
if ($CompareTo) {
    $previous = Get-Content -LiteralPath $CompareTo -Raw | ConvertFrom-Json
    $previousAdditionalFileCount = if ($previous.PSObject.Properties.Name -contains 'AdditionalFiles') {
        $previous.AdditionalFiles.Count
    }
    else { 0 }
    if ($previous.SchemaVersion -ne 1 -or $previous.Roots.Count -ne $resolvedRoots.Count -or
        $previousAdditionalFileCount -ne $resolvedFiles.Count) {
        throw 'The comparison manifest has an incompatible schema or artifact category count.'
    }
    $oldFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($file in $previous.Files) { $oldFiles.Add($file.Path, $file) }
    $added = [Collections.Generic.List[string]]::new()
    $removed = [Collections.Generic.List[string]]::new()
    $changed = [Collections.Generic.List[string]]::new()
    $rewritten = [Collections.Generic.List[string]]::new()
    foreach ($entry in $artifactEntries.GetEnumerator()) {
        if (-not $oldFiles.ContainsKey($entry.Key)) { $added.Add($entry.Key); continue }
        $old = $oldFiles[$entry.Key]
        $contentChanged = $old.Sha256 -cne $entry.Value.Sha256 -or $old.Bytes -ne $entry.Value.Bytes
        if ($contentChanged) { $changed.Add($entry.Key) }
        if ($contentChanged -or $old.LastWriteTimeUtcTicks -ne $entry.Value.LastWriteTimeUtcTicks) {
            $rewritten.Add($entry.Key)
        }
    }
    foreach ($key in $oldFiles.Keys) {
        if (-not $artifactEntries.ContainsKey($key)) { $removed.Add($key) }
    }
    $removed.Sort([StringComparer]::Ordinal)
    $comparison = [ordered]@{
        PreviousManifestSha256 = (Get-FileHash -LiteralPath $CompareTo -Algorithm SHA256).Hash
        EqualContent = $added.Count -eq 0 -and $removed.Count -eq 0 -and $changed.Count -eq 0
        Added = @($added.ToArray())
        Removed = @($removed.ToArray())
        ContentChanged = @($changed.ToArray())
        ObservedRewritten = @($rewritten.ToArray())
    }
}

[long] $bytes = 0
foreach ($file in $artifactEntries.Values) { $bytes += $file.Bytes }
$snapshot = [ordered]@{
    SchemaVersion = 1
    CapturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Roots = $resolvedRoots
    AdditionalFiles = $resolvedFiles
    FileCount = $artifactEntries.Count
    TotalBytes = $bytes
    Files = @($artifactEntries.Values)
    Comparison = $comparison
    Limitations = @(
        'Root positions are semantic identities; compare the same ordered categories even when absolute paths differ.',
        'Observed rewrites use timestamps and content, not a filesystem write trace; restored timestamps can hide identical rewrites.',
        'A multi-file manifest is not an atomic snapshot. Stop writers for the entire collection.',
        'Byte equality is valid for unchanged source partitioning; it does not prove native ABI or lifetime equivalence after repartitioning.'
    )
}
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report))
$stream = [IO.File]::Open($report, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $json = [Text.UTF8Encoding]::new($false).GetBytes(($snapshot | ConvertTo-Json -Depth 7))
    $stream.Write($json, 0, $json.Length)
}
finally { $stream.Dispose() }
Write-Output "Artifact manifest: $report"
