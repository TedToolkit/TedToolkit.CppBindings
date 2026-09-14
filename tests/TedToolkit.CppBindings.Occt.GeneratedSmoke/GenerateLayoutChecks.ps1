param(
    [Parameter(Mandatory)][string] $InputPath,
    [Parameter(Mandatory)][string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$inventory = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json
$layouts = @($inventory.Records)
if ($layouts.Count -eq 0 -or @($layouts | Where-Object IsGeneric).Count -eq 0) {
    throw 'The complete prepared native layout inventory, including closed generics, is required.'
}
$source = [Collections.Generic.List[string]]::new()
$source.Add("using $($inventory.Namespace);")
$source.Add('internal static class PreparedLayoutProbe { public static void Run() {')
for ($index = 0; $index -lt $layouts.Count; $index += 100) {
    $source.Add("Batch$index();")
}
$source.Add('System.Console.WriteLine("Prepared native layouts checked: ' + $layouts.Count +
    '; closed generic specializations: ' + @($layouts | Where-Object IsGeneric).Count + '."); }')
for ($index = 0; $index -lt $layouts.Count; $index += 100) {
    $source.Add("private static void Batch$index() {")
    for ($entry = $index; $entry -lt [Math]::Min($index + 100, $layouts.Count); $entry++) {
        $layout = $layouts[$entry]
        if ($layout.Size -le 0 -or $layout.Alignment -notin @(1, 2, 4, 8)) {
            throw "Invalid prepared native layout: $($layout.NativeType)"
        }
        $source.Add("GeneratedLayoutProbe.Check<$($layout.ManagedType)>($($layout.Size), $($layout.Alignment));")
    }
    $source.Add('}')
}
$source.Add('}')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
[IO.File]::WriteAllLines($OutputPath, $source, [Text.UTF8Encoding]::new($false))
