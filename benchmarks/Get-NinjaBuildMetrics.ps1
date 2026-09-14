param(
    [Parameter(Mandatory)] [string] $LogPath,
    [Parameter(Mandatory)] [string] $ReportPath,
    [string] $BeforeLogPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$report = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $report) { throw 'Refusing to overwrite a native metrics report.' }

function Read-LogSnapshot {
    param([string] $Path)
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $stream = [IO.File]::Open($resolved, 'Open', 'Read', 'Read')
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.UTF8Encoding]::new($false, $true))
        try { $content = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
    if ($content -notmatch '\A# ninja log v(5|7)\r?\n' -or -not $content.EndsWith("`n")) {
        throw 'Expected a complete Ninja v5 or v7 log snapshot.'
    }
    return [pscustomobject]@{
        Path = $resolved
        Content = $content
        ContentSha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($content)))
    }
}

$after = Read-LogSnapshot $LogPath
$before = $null
$delta = $after.Content.Substring($after.Content.IndexOf("`n") + 1)
if ($BeforeLogPath) {
    $before = Read-LogSnapshot $BeforeLogPath
    if (-not $after.Content.StartsWith($before.Content, [StringComparison]::Ordinal)) {
        throw 'Ninja log was replaced, truncated, or recompacted; the sample cannot be attributed.'
    }
    $delta = $after.Content.Substring($before.Content.Length)
}

# Ninja records one row per output, so a DLL and its import library can be one command.
$commands = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$outputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$rows = 0
foreach ($line in $delta.Split("`n", [StringSplitOptions]::RemoveEmptyEntries)) {
    $parts = $line.TrimEnd("`r").Split("`t")
    $start = 0L
    $end = 0L
    if ($parts.Count -ne 5 -or
        -not [long]::TryParse($parts[0], [ref] $start) -or
        -not [long]::TryParse($parts[1], [ref] $end) -or
        $start -lt 0 -or $end -lt $start -or
        $parts[2] -notmatch '^\d+$' -or [string]::IsNullOrWhiteSpace($parts[3]) -or
        $parts[4] -notmatch '^[0-9a-fA-F]+$') {
        throw 'Malformed Ninja timing row.'
    }
    if (-not $outputs.Add($parts[3])) {
        throw 'The selected log repeats an output; use per-invocation snapshots.'
    }
    $rows++
    $key = "$start/$end/$($parts[4])"
    if (-not $commands.ContainsKey($key)) {
        $commands.Add($key, [pscustomobject]@{
            StartMilliseconds = $start
            EndMilliseconds = $end
            DurationMilliseconds = $end - $start
            CommandHash = $parts[4]
            Outputs = [Collections.Generic.List[string]]::new()
        })
    }
    $commands[$key].Outputs.Add($parts[3])
}
$entries = @($commands.Values | Sort-Object StartMilliseconds, EndMilliseconds, CommandHash)
$compile = @($entries | Where-Object { @($_.Outputs | Where-Object { $_ -match '\.(obj|o)$' }).Count -gt 0 })
$link = @($entries | Where-Object { @($_.Outputs | Where-Object { $_ -match '\.(dll|exe|so|dylib)$' }).Count -gt 0 })
$span = 0L
$aggregate = 0L
foreach ($entry in $entries) { $aggregate += $entry.DurationMilliseconds }
if ($entries.Count -gt 0) {
    $span = ($entries | Measure-Object EndMilliseconds -Maximum).Maximum -
        ($entries | Measure-Object StartMilliseconds -Minimum).Minimum
}
$result = [ordered]@{
    SchemaVersion = 1
    AfterLog = $after.Path
    AfterContentSha256 = $after.ContentSha256
    BeforeLog = if ($null -ne $before) { $before.Path } else { $null }
    BeforeContentSha256 = if ($null -ne $before) { $before.ContentSha256 } else { $null }
    OutputRows = $rows
    CommandCount = $entries.Count
    CompileCommandCount = $compile.Count
    LinkCommandCount = $link.Count
    LoggedSpanMilliseconds = $span
    AggregateCommandMilliseconds = $aggregate
    LongestCompileCommands = @($compile | Sort-Object DurationMilliseconds -Descending | Select-Object -First 20)
    LinkCommands = $link
    Commands = $entries
    Limitations = @(
        'Use a fresh log or before/after snapshots covering exactly one invocation; stop writers before capture.',
        'Prefix comparison rejects truncation/recompaction but cannot prove invocation boundaries.',
        'Command identity uses timing and command hash; indistinguishable commands may be coalesced.',
        'Object outputs classify compile commands, not source dependencies; custom multi-object rules need separate inventory.',
        'Logged span excludes unlogged setup and failure; aggregate command time overlaps and is not wall time.',
        'These timings do not prove critical path, CPU time, successful build, or benchmark validity.'
    )
}
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report))
$json = $result | ConvertTo-Json -Depth 8
$outputStream = [IO.File]::Open($report, 'CreateNew', 'Write', 'None')
try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $outputStream.Write($bytes)
}
finally { $outputStream.Dispose() }
Write-Output "Native metrics: $($compile.Count) compile commands, $($link.Count) link commands; $report"
