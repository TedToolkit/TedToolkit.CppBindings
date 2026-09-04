#Requires -Version 7.5
param(
    [Parameter(Mandatory)] [string] $ReceiptPath,
    [Parameter(Mandatory)] [ValidateSet('baseline', 'candidate')] [string] $Variant,
    [Parameter(Mandatory)] [ValidateSet('original', 'changed')] [string] $State,
    [Parameter(Mandatory)] [string] $SourceRepositoryRoot,
    [Parameter(Mandatory)] [string] $SourceBaseRevision,
    [Parameter(Mandatory)] [string] $HostDirectory,
    [Parameter(Mandatory)] [string] $HostEntryPointRelativePath,
    [Parameter(Mandatory)] [string] $PublishCompletionReceiptPath,
    [string] $FrozenPatchPath,
    [switch] $FixtureOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-Git {
    param([string[]] $Arguments)
    $output = @(& git -c "safe.directory=$sourceRoot" -C $sourceRoot @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "git failed while creating host provenance: $($Arguments -join ' ')" }
    return $output
}

function Get-GitPatchBytes {
    param([string] $Base, [string] $Head)
    $lines = @(Invoke-Git @('diff', '--binary', '--full-index', '--no-ext-diff', $Base, $Head, '--'))
    if ($lines.Count -eq 0) { return ,([byte[]]::new(0)) }
    return ,($utf8.GetBytes(($lines -join "`n") + "`n"))
}

function Assert-ManagedConsoleHost {
    param([string] $Path)
    try { $name = [Reflection.AssemblyName]::GetAssemblyName($Path) }
    catch { throw "The host entry point is not a valid managed assembly: $Path" }
    if ($name.Name -cne 'TedToolkit.CppBindings.Occt.Console') {
        throw "Unexpected host assembly identity: $($name.Name)"
    }
    $stem = [IO.Path]::GetFileNameWithoutExtension($Path)
    foreach ($suffix in @('.deps.json', '.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($Path)) ($stem + $suffix)) -PathType Leaf)) {
            throw "The complete Console host is missing $stem$suffix."
        }
    }
    return $name.FullName
}

$receipt = [IO.Path]::GetFullPath($ReceiptPath)
if (Test-Path -LiteralPath $receipt) { throw 'Refusing to overwrite a host receipt.' }
$sourceRoot = (Resolve-Path -LiteralPath $SourceRepositoryRoot).Path
$hostRoot = (Resolve-Path -LiteralPath $HostDirectory).Path
$entryPoint = [IO.Path]::GetFullPath((Join-Path $hostRoot $HostEntryPointRelativePath))
$hostPrefix = $hostRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $entryPoint.StartsWith($hostPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $entryPoint -PathType Leaf)) {
    throw 'The host entry point must be a file inside HostDirectory.'
}
$assemblyIdentity = Assert-ManagedConsoleHost $entryPoint
if ($SourceBaseRevision -notmatch '^[0-9a-f]{40}$') { throw 'SourceBaseRevision must be a lowercase full Git SHA.' }
$sourceRevision = ((Invoke-Git @('rev-parse', 'HEAD')) -join '').Trim()
$status = @(Invoke-Git @('status', '--porcelain=v1', '--untracked-files=all', '--ignore-submodules=all'))
if ($status.Count -ne 0) { throw 'Host provenance requires a clean source repository.' }

$patchHash = $null
$patchPath = $null
$deltaBytes = Get-GitPatchBytes $SourceBaseRevision $sourceRevision
if ($State -eq 'original') {
    if ($sourceRevision -cne $SourceBaseRevision -or $deltaBytes.Length -ne 0 -or $FrozenPatchPath) {
        throw 'An original host must come from the exact clean base revision with no patch.'
    }
}
else {
    if (-not $FrozenPatchPath) { throw 'A changed host requires the frozen source patch.' }
    $patchPath = (Resolve-Path -LiteralPath $FrozenPatchPath).Path
    $patchBytes = [IO.File]::ReadAllBytes($patchPath)
    if ($patchBytes.Length -eq 0 -or $patchBytes.Length -ne $deltaBytes.Length -or
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($patchBytes)) -cne
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($deltaBytes))) {
        throw 'The changed-host source delta is not exactly the frozen patch.'
    }
    $patchHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($patchBytes))
}

$publishReceiptPath = (Resolve-Path -LiteralPath $PublishCompletionReceiptPath).Path
$publish = Get-Content -LiteralPath $publishReceiptPath -Raw | ConvertFrom-Json -AsHashtable -DateKind String
$expectedProject = [IO.Path]::GetFullPath((Join-Path $sourceRoot 'tests/TedToolkit.CppBindings.Occt.Console/TedToolkit.CppBindings.Occt.Console.csproj'))
$expectedArguments = @('publish', $expectedProject, '--configuration', 'Release', '--framework', 'net10.0',
    '--no-restore', '--output', $hostRoot, '--nologo')
if ($publish.SchemaVersion -ne 1 -or $publish.ReceiptKind -cne 'occt-console-host-publish' -or
    -not $publish.Succeeded -or $publish.ExitCode -ne 0 -or -not $publish.FreshHostDirectory -or
    -not $publish.SourceCleanBeforeAndAfter -or $publish.SourceRevision -cne $sourceRevision -or
    -not [IO.Path]::GetFullPath($publish.SourceRepositoryRoot).Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFullPath($publish.ProjectPath).Equals($expectedProject, [StringComparison]::OrdinalIgnoreCase) -or
    $publish.ProjectRelativePath -cne 'tests/TedToolkit.CppBindings.Occt.Console/TedToolkit.CppBindings.Occt.Console.csproj' -or
    (Get-FileHash -LiteralPath $expectedProject -Algorithm SHA256).Hash -cne $publish.ProjectSha256 -or
    -not [IO.Path]::GetFullPath($publish.HostDirectory).Equals($hostRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $publish.HostEntryPointRelativePath -cne $HostEntryPointRelativePath.Replace('\', '/') -or
    -not [IO.Path]::GetFullPath($publish.Command.WorkingDirectory).Equals($sourceRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $publish.Command.Executable -cne $publish.DotNetPath -or
    (@($publish.Command.Arguments) -join "`n") -cne ($expectedArguments -join "`n") -or
    $publish.Output -isnot [hashtable] -or $publish.Output.StandardOutput -isnot [string] -or
    $publish.Output.StandardError -isnot [string] -or $publish.HostFiles -isnot [array] -or
    $publish.HostAssemblyIdentity -cne $assemblyIdentity) {
    throw 'The host publish completion receipt does not prove the exact fresh build relationship.'
}
foreach ($bindingName in @('DotNet', 'PublishWrapper')) {
    $boundPath = (Resolve-Path -LiteralPath $publish["${bindingName}Path"]).Path
    if ((Get-FileHash -LiteralPath $boundPath -Algorithm SHA256).Hash -cne $publish["${bindingName}Sha256"]) {
        throw "The host publish $bindingName binding changed."
    }
}

$files = [Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $hostRoot -Recurse -File | Sort-Object FullName) {
    $files.Add([ordered]@{
        Path = [IO.Path]::GetRelativePath($hostRoot, $file.FullName).Replace('\', '/')
        Bytes = $file.Length
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    })
}
if ($files.Count -lt 3) { throw 'A complete Console host receipt requires the entry point and runtime metadata.' }
if ($files.Count -ne $publish.HostFiles.Count) { throw 'The fresh publish output inventory changed.' }
for ($index = 0; $index -lt $files.Count; $index++) {
    if ($files[$index].Path -cne $publish.HostFiles[$index].Path -or
        $files[$index].Bytes -ne $publish.HostFiles[$index].Bytes -or
        $files[$index].Sha256 -cne $publish.HostFiles[$index].Sha256) {
        throw "The fresh publish output changed: $($files[$index].Path)"
    }
}

$document = [ordered]@{
    SchemaVersion = 2
    ReceiptKind = 'occt-console-host'
    FixtureOnly = [bool] $FixtureOnly
    Variant = $Variant
    State = $State
    SourceRepositoryRoot = $sourceRoot
    SourceBaseRevision = $SourceBaseRevision
    SourceRevision = $sourceRevision
    SourceClean = $true
    FrozenPatchPath = $patchPath
    FrozenPatchSha256 = $patchHash
    SourceDeltaSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($deltaBytes))
    PublishCompletionReceiptPath = $publishReceiptPath
    PublishCompletionReceiptSha256 = (Get-FileHash -LiteralPath $publishReceiptPath -Algorithm SHA256).Hash
    PublishWrapperPath = $publish.PublishWrapperPath
    PublishWrapperSha256 = $publish.PublishWrapperSha256
    HostDirectory = $hostRoot
    HostEntryPointRelativePath = $HostEntryPointRelativePath.Replace('\', '/')
    HostAssemblyIdentity = $assemblyIdentity
    HostFiles = @($files.ToArray())
}
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt))
$bytes = $utf8.GetBytes(($document | ConvertTo-Json -Depth 8))
$stream = [IO.File]::Open($receipt, 'CreateNew', 'Write', 'None')
try { $stream.Write($bytes) }
finally { $stream.Dispose() }
Write-Output "Console host receipt: $receipt"
