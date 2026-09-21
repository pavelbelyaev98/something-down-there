param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../unity'))
$log = Join-Path $project 'Logs/ContentSync.log'

if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw 'Unity editor not found. Pass -UnityEditor with the full Unity executable path.'
}

New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$arguments = @(
    '-batchmode', '-quit', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'SomethingDownThere.Editor.DiscoveryContentSetup.SyncBatch',
    '-logFile', ('"' + $log + '"')
)

$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Content sync failed (exit $($process.ExitCode)). Read $log" }

Write-Output "Content synced. Log: $log"
