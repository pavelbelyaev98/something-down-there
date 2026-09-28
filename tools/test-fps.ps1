param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe',
    [ValidateSet('All', 'EditMode', 'PlayMode')][string]$Mode = 'All',
    [string]$Filter = '',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../unity')
)

$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw 'Unity editor not found. Pass -UnityEditor with the full path to Unity 6000.6.0f1.'
}
$taskRunDirectory = Join-Path $taskProject ('Logs/FpsValidation-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $taskRunDirectory -Force | Out-Null
$taskModes = if ($Mode -eq 'All') { @('EditMode', 'PlayMode') } else { @($Mode) }
foreach ($taskMode in $taskModes) {
    $taskResultPath = Join-Path $taskRunDirectory ($taskMode + '.xml')
    $taskLogPath = Join-Path $taskRunDirectory ($taskMode + '.log')
    $taskArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $taskProject + '"'),
        '-runTests', '-testPlatform', $taskMode, '-assemblyNames', ('SomethingDownThere.' + $taskMode + 'Tests'),
        '-testResults', ('"' + $taskResultPath + '"'), '-logFile', ('"' + $taskLogPath + '"'))
    if ($Filter) { $taskArguments += @('-testFilter', $Filter) }
    Write-Output "Running $taskMode tests. Log: $taskLogPath"
    $taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if (-not (Test-Path -LiteralPath $taskResultPath -PathType Leaf)) {
        throw "Unity exited without test results (exit $($taskProcess.ExitCode)). Read $taskLogPath"
    }
    [xml]$taskXml = Get-Content -LiteralPath $taskResultPath -Raw
    $taskRun = $taskXml.'test-run'
    Write-Output "$taskMode : $($taskRun.result), $($taskRun.passed)/$($taskRun.total) passed. Results: $taskResultPath"
    # An ignored test marks the run 'Skipped:Ignored'; like the live-Editor path, only failures fail it.
    $taskIgnoredOnly = $taskRun.result -eq 'Skipped:Ignored' -and [int]$taskRun.failed -eq 0 -and [int]$taskRun.passed -gt 0
    if (($taskProcess.ExitCode -ne 0 -and -not $taskIgnoredOnly) -or ($taskRun.result -ne 'Passed' -and -not $taskIgnoredOnly) -or [int]$taskRun.total -eq 0) {
        throw "FPS validation failed. Read $taskResultPath and $taskLogPath"
    }
}
