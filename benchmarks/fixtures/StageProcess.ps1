param(
    [ValidateSet('echo', 'failure', 'sleep', 'tree', 'delay')]
    [string] $Mode,
    [string] $Value
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
switch ($Mode) {
    'echo' {
        [Console]::Out.WriteLine($Value)
        [Console]::Error.WriteLine('stderr retained')
        [Console]::Out.WriteLine(('x' * 1048576))
        Start-Sleep -Milliseconds 600
    }
    'failure' { [Console]::Error.WriteLine('controlled failure'); exit 17 }
    'delay' { Start-Sleep -Milliseconds ([int] $Value) }
    'sleep' { Start-Sleep -Seconds 30 }
    'tree' {
        $child = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @(
            '-NoProfile', '-File', ('"' + $PSCommandPath + '"'), '-Mode', 'sleep'
        ) -WindowStyle Hidden -PassThru
        [Console]::Out.WriteLine("child:$($child.Id)")
        Start-Sleep -Seconds 30
    }
}
