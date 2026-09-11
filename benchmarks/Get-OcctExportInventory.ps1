#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [Parameter(Mandatory)] [string] $ReportPath,
    [string] $CompareTo
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'OcctExportInventory.ps1')
$source = (Resolve-Path -LiteralPath $SourcePath).Path
$report = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $report) { throw 'Refusing to overwrite an export inventory.' }
$bytes = [IO.File]::ReadAllBytes($source)
$text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes).Replace("`r`n", "`n")
$declarations = [Collections.Generic.List[string]]::new()
$slots = [Collections.Generic.List[string]]::new()
foreach ($line in $text.Split("`n")) {
    if ($line -cmatch '^extern "C" void ([A-Za-z_][A-Za-z0-9_]*)\(\);$') {
        $declarations.Add($Matches[1])
    }
    elseif ($line -cmatch '^    reinterpret_cast<std::uintptr_t>\(&([A-Za-z_][A-Za-z0-9_]*)\),$') {
        $slots.Add($Matches[1])
    }
}
if ($declarations.Count -eq 0 -or -not (Test-OcctExportSequenceEqual $declarations $slots)) {
    throw 'NativeFunctionTable.cpp does not contain one matching ordered declaration/slot inventory.'
}
if ($text -notmatch 'extern "C" __declspec\(dllexport\) const std::uintptr_t\* NativeApi_GetFunctionTable\(\) noexcept') {
    throw 'NativeFunctionTable.cpp is missing the sole function-table export.'
}
$uniqueDeclarations = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($declaration in $declarations) {
    if (-not $uniqueDeclarations.Add($declaration)) {
        throw 'NativeFunctionTable.cpp contains duplicate slots.'
    }
}

$comparison = $null
if ($CompareTo) {
    try { $previous = Read-OcctExportInventoryFile $CompareTo }
    catch {
        throw 'The comparison export inventory is invalid.'
    }
    $comparison = [ordered]@{
        PreviousInventorySha256 = (Get-FileHash -LiteralPath $CompareTo -Algorithm SHA256).Hash
        EqualOrderedExports = Test-OcctExportSequenceEqual $previous.Exports $declarations
    }
}

$result = [ordered]@{
    SchemaVersion = 1
    SourceFile = $source
    SourceFileSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    ExportCount = $declarations.Count
    Exports = @($declarations.ToArray())
    Comparison = $comparison
}
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report))
$json = [Text.UTF8Encoding]::new($false).GetBytes(($result | ConvertTo-Json -Depth 5))
$stream = [IO.File]::Open($report, 'CreateNew', 'Write', 'None')
try { $stream.Write($json) }
finally { $stream.Dispose() }
Write-Output "Export inventory: $report"
