function Read-OcctExportInventoryFile {
    param([Parameter(Mandatory)] [string] $Path)

    $inventory = Get-Content -LiteralPath $Path -Raw |
        ConvertFrom-Json -AsHashtable -DateKind String
    $expectedMembers = @('SchemaVersion', 'SourceFile', 'SourceFileSha256', 'ExportCount', 'Exports', 'Comparison')
    if ($inventory -isnot [Collections.IDictionary] -or $inventory.Count -ne $expectedMembers.Count -or
        @($expectedMembers | Where-Object { -not $inventory.ContainsKey($_) }).Count -ne 0 -or
        $inventory.SchemaVersion -isnot [long] -or $inventory.SchemaVersion -ne 1 -or
        $inventory.SourceFile -isnot [string] -or [string]::IsNullOrWhiteSpace($inventory.SourceFile) -or
        $inventory.SourceFileSha256 -isnot [string] -or $inventory.SourceFileSha256 -cnotmatch '\A[0-9A-Fa-f]{64}\z' -or
        $inventory.ExportCount -isnot [long] -or $inventory.ExportCount -lt 0 -or
        $inventory.Exports -isnot [array] -or $inventory.Exports.Count -ne $inventory.ExportCount) {
        throw "Invalid OCCT export inventory schema: $Path"
    }

    $uniqueExports = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($export in $inventory.Exports) {
        if ($export -isnot [string] -or $export -cnotmatch '\A[A-Za-z_][A-Za-z0-9_]*\z' -or
            -not $uniqueExports.Add($export)) {
            throw "Invalid OCCT export inventory symbol: $Path"
        }
    }

    if ($null -ne $inventory.Comparison) {
        $comparisonMembers = @('PreviousInventorySha256', 'EqualOrderedExports')
        if ($inventory.Comparison -isnot [Collections.IDictionary] -or
            $inventory.Comparison.Count -ne $comparisonMembers.Count -or
            @($comparisonMembers | Where-Object { -not $inventory.Comparison.ContainsKey($_) }).Count -ne 0 -or
            $inventory.Comparison.PreviousInventorySha256 -isnot [string] -or
            $inventory.Comparison.PreviousInventorySha256 -cnotmatch '\A[0-9A-Fa-f]{64}\z' -or
            $inventory.Comparison.EqualOrderedExports -isnot [bool]) {
            throw "Invalid OCCT export inventory comparison: $Path"
        }
    }
    return $inventory
}

function Test-OcctExportSequenceEqual {
    param([Parameter(Mandatory)] $Left, [Parameter(Mandatory)] $Right)

    if ($Left.Count -ne $Right.Count) { return $false }
    for ($index = 0; $index -lt $Left.Count; $index++) {
        if (-not [StringComparer]::Ordinal.Equals($Left[$index], $Right[$index])) { return $false }
    }
    return $true
}
