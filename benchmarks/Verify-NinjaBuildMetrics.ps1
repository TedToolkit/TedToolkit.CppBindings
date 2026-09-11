$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runner = Join-Path $PSScriptRoot 'Get-NinjaBuildMetrics.ps1'
$root = Join-Path (Split-Path $PSScriptRoot) "out/benchmark/ninja-proof-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $root
$header = "# ninja log v7`n"
$old = "0`t900`t1`told.cpp.obj`taa`n"
$build = "0`t100`t2`tfirst.cpp.obj`tab`n50`t150`t3`tsecond.cpp.obj`tac`n150`t180`t4`tapp.dll`tad`n150`t180`t4`tapp.lib`tad`n"
function Write-Fixture {
    param([string] $Name, [string] $Content)
    $path = Join-Path $root $Name
    [IO.File]::WriteAllText($path, $Content)
    return $path
}
function Assert-Rejected {
    param([string] $Name, [string] $Content, [string] $Before, [string] $Message)
    $path = Write-Fixture "$Name.log" $Content
    $destination = Join-Path $root "$Name.json"
    $caught = $false
    try { & $runner -LogPath $path -ReportPath $destination -BeforeLogPath $Before }
    catch { $caught = $_.Exception.Message -like $Message }
    if (-not $caught -or (Test-Path -LiteralPath $destination)) { throw "Expected rejection: $Name" }
}
$before = Write-Fixture 'before.log' ($header + $old)
$after = Write-Fixture 'after.log' ($header + $old + $build)
$report = Join-Path $root 'delta.json'
& $runner -LogPath $after -BeforeLogPath $before -ReportPath $report
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($result.OutputRows -ne 4 -or $result.CommandCount -ne 3 -or
    $result.CompileCommandCount -ne 2 -or $result.LinkCommandCount -ne 1 -or
    $result.AggregateCommandMilliseconds -ne 230 -or $result.LoggedSpanMilliseconds -ne 180 -or
    $result.LinkCommands[0].Outputs.Count -ne 2) { throw 'Incorrect delta or multi-output accounting.' }
$emptyReport = Join-Path $root 'unchanged.json'
& $runner -LogPath $after -BeforeLogPath $after -ReportPath $emptyReport
$empty = Get-Content -LiteralPath $emptyReport -Raw | ConvertFrom-Json
if ($empty.CommandCount -ne 0 -or $empty.LoggedSpanMilliseconds -ne 0) { throw 'Unchanged build was not empty.' }
$fresh = Write-Fixture 'fresh.log' (($header + $build).Replace('v7', 'v5'))
& $runner -LogPath $fresh -ReportPath (Join-Path $root 'fresh.json')
Assert-Rejected 'recompacted' ($header + $build) $before '*recompacted*'
Assert-Rejected 'truncated' ($header + $build.TrimEnd("`n")) '' '*complete Ninja*'
Assert-Rejected 'negative' ($header + "-1`t10`t2`tx.obj`tab`n") '' '*Malformed*'
Assert-Rejected 'reversed' ($header + "11`t10`t2`tx.obj`tab`n") '' '*Malformed*'
Assert-Rejected 'malformed' ($header + "0`t10`t2`tx.obj`tinvalid`n") '' '*Malformed*'
Assert-Rejected 'unsupported' ($header.Replace('v7', 'v99') + $build) '' '*complete Ninja*'
Assert-Rejected 'duplicate' ($header + $build + $build) '' '*repeats an output*'
# Parallel completion rows need not be ordered by their recorded end timestamp.
$unordered = Write-Fixture 'unordered.log' ($header + $old + $build)
$unorderedReport = Join-Path $root 'unordered.json'
& $runner -LogPath $unordered -ReportPath $unorderedReport
$unorderedResult = Get-Content -LiteralPath $unorderedReport -Raw | ConvertFrom-Json
if ($unorderedResult.CompileCommandCount -ne 3 -or $unorderedResult.LoggedSpanMilliseconds -ne 900) {
    throw 'Out-of-order timing rows were not accounted for.'
}
$originalHash = (Get-FileHash -LiteralPath $report).Hash
$rejected = $false
try { & $runner -LogPath $after -ReportPath $report }
catch { $rejected = $_.Exception.Message -like 'Refusing to overwrite*' }
if (-not $rejected -or (Get-FileHash -LiteralPath $report).Hash -ne $originalHash) {
    throw 'Report overwrite protection failed.'
}
Write-Output "Ninja metrics proof passed: delta, multi-output, unchanged, fresh v5, out-of-order rows, seven invalid inputs, and non-overwrite; $root"
