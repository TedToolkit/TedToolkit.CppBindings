param([string] $Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$assemblyRoot = Join-Path $PSScriptRoot "bin\$Configuration\net10.0"
if ([Environment]::Version.Major -lt 10) {
    throw 'This reflection-based build-gate proof requires PowerShell running on .NET 10 or later.'
}

[Reflection.Assembly]::LoadFrom((Join-Path $assemblyRoot 'ModularPipelines.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $assemblyRoot 'Build.dll'))
$validate = $assembly.GetType('ManagedTestGateModule').GetMethod('EnsureSuccessful', [Reflection.BindingFlags]'Static,NonPublic')
$run = $assembly.GetType('BuildProcess').GetMethod('RunAsync', [Reflection.BindingFlags]'Static,NonPublic')
$reportPath = Join-Path ([IO.Path]::GetTempPath()) ('occt-trx-proof-' + [Guid]::NewGuid().ToString('N') + '.trx')
$scenarios = @(
    @{ Name = 'all passed'; Xml = '<TestRun><Counters total="2" executed="2" passed="2" /></TestRun>'; Success = $true },
    @{ Name = 'no tests'; Xml = '<TestRun><Counters total="0" executed="0" passed="0" /></TestRun>'; Success = $false },
    @{ Name = 'failed test'; Xml = '<TestRun><Counters total="2" executed="2" passed="1" /></TestRun>'; Success = $false },
    @{ Name = 'skipped test'; Xml = '<TestRun><Counters total="2" executed="1" passed="1" /></TestRun>'; Success = $false },
    @{ Name = 'missing counters'; Xml = '<TestRun />'; Success = $false },
    @{ Name = 'missing attribute'; Xml = '<TestRun><Counters total="1" executed="1" /></TestRun>'; Success = $false },
    @{ Name = 'malformed counter'; Xml = '<TestRun><Counters total="x" executed="1" passed="1" /></TestRun>'; Success = $false }
)

try {
    foreach ($scenario in $scenarios) {
        Set-Content -LiteralPath $reportPath -Value $scenario.Xml
        $success = $true
        try { $validate.Invoke($null, @([IO.FileInfo]::new($reportPath))) } catch { $success = $false }
        if ($success -ne $scenario.Success) {
            throw "Unexpected result for TRX scenario: $($scenario.Name)."
        }
    }

    $failed = $false
    try {
        $task = $run.Invoke($null, @('pwsh', [string[]]@('-NoProfile', '-Command', 'exit 17'), $PSScriptRoot, [Threading.CancellationToken]::None))
        $task.GetAwaiter().GetResult()
    }
    catch { $failed = $_.Exception.GetBaseException().Message -like '*exited with code 17*' }
    if (-not $failed) { throw 'A nonzero child exit must fail the build gate.' }

    $cancellation = [Threading.CancellationTokenSource]::new()
    try {
        $cancelled = $false
        try {
            $task = $run.Invoke($null, @('pwsh', [string[]]@('-NoProfile', '-Command', 'Start-Sleep -Seconds 30'), $PSScriptRoot, $cancellation.Token))
            $cancellation.Cancel()
            $task.GetAwaiter().GetResult()
        }
        catch { $cancelled = $_.Exception.GetBaseException() -is [OperationCanceledException] }
        if (-not $cancelled) { throw 'Cancellation must terminate the child and fail the build gate.' }
    }
    finally { $cancellation.Dispose() }

    Write-Output 'Managed gate proof passed: seven TRX cases, nonzero child exit, and cancellation.'
}
finally {
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
}
