param(
    [Parameter(Mandatory)][string]$Unity,
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Output,
    [Parameter(Mandatory)][string]$Filter,
    [int]$TimeoutSeconds = 900,
    [switch]$RestoreOnly
)
$ErrorActionPreference = 'Stop'
$taskBackup = Join-Path $Output 'playerprefs-backup.clixml'
$taskPaths = @('Software\Unity\UnityEditor\Life Up Studio\Subject#42', 'Software\Life Up Studio\Subject#42')
function Restore-TaskPreferences {
    if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before restoring PlayerPrefs.' }
    $taskSaved = Import-Clixml -LiteralPath $taskBackup
    foreach ($taskEntry in $taskSaved) {
        if ($taskEntry.Exists -eq $false) {
            [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($taskEntry.Path, $false)
            continue
        }
        $taskKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($taskEntry.Path)
        try {
            foreach ($taskName in $taskKey.GetValueNames()) {
                if ($taskName -notin @($taskEntry.Values | ForEach-Object { $_.Name })) { $taskKey.DeleteValue($taskName, $false) }
            }
            foreach ($taskValue in $taskEntry.Values) {
                $taskKey.SetValue($taskValue.Name, $taskValue.Value, [Microsoft.Win32.RegistryValueKind]$taskValue.Kind)
            }
            foreach ($taskValue in $taskEntry.Values) {
                $taskActual = $taskKey.GetValue($taskValue.Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                if ([int]$taskKey.GetValueKind($taskValue.Name) -ne $taskValue.Kind -or
                    (ConvertTo-Json -InputObject $taskActual -Compress -Depth 8) -cne (ConvertTo-Json -InputObject $taskValue.Value -Compress -Depth 8)) {
                    throw 'PlayerPrefs restoration mismatch; keeping the recovery snapshot.'
                }
            }
        } finally { $taskKey.Dispose() }
    }
    $taskFontBackup = Join-Path $Output 'damage-font-before.bin'
    if (Test-Path -LiteralPath $taskFontBackup) {
        [IO.File]::WriteAllBytes((Join-Path $Project 'Assets/_Project/Fonts/Subject42 Damage Numbers.mat'), [IO.File]::ReadAllBytes($taskFontBackup))
    }
    [pscustomobject]@{ Restored = $true; VerifiedValues = @($taskSaved | ForEach-Object { $_.Values }).Count; AtUtc = [DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'playerprefs-restore-verification.json') -Encoding utf8
    Move-Item -LiteralPath $taskBackup -Destination (Join-Path $Output 'playerprefs-restored.clixml') -Force
}
if ($RestoreOnly) { Restore-TaskPreferences; exit 0 }
$taskAllBackups = @(Get-ChildItem -LiteralPath (Join-Path $Project 'Artifacts/GeneratedQA/NightShift') -Filter 'playerprefs-backup.clixml' -File -Recurse -ErrorAction SilentlyContinue)
if ($taskAllBackups.Count -gt 0) { throw "Unrestored PlayerPrefs snapshot exists: $($taskAllBackups[0].FullName)" }
if (Test-Path -LiteralPath $taskBackup) { throw "Unrestored PlayerPrefs snapshot: $taskBackup. Run -RestoreOnly before more testing." }
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity Editor is already open; refusing concurrent Editor use.' }
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity not found: $Unity" }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$taskSaved = foreach ($taskPath in $taskPaths) {
    $taskKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($taskPath)
    $taskValues = @()
    if ($null -ne $taskKey) {
        try {
            $taskValues = @(foreach ($taskName in $taskKey.GetValueNames()) {
                [pscustomobject]@{ Name = $taskName; Value = $taskKey.GetValue($taskName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); Kind = [int]$taskKey.GetValueKind($taskName) }
            })
        } finally { $taskKey.Dispose() }
    }
    [pscustomobject]@{ Path = $taskPath; Values = $taskValues; Exists = ($null -ne $taskKey) }
}
$taskSaved | Export-Clixml -LiteralPath $taskBackup
# TMP updates shared serialized material ratios during Play Mode. Preserve the
# worker's authored input, then discard this known test side effect only.
[IO.File]::WriteAllBytes((Join-Path $Output 'damage-font-before.bin'), [IO.File]::ReadAllBytes((Join-Path $Project 'Assets/_Project/Fonts/Subject42 Damage Numbers.mat')))
$taskProcess = $null
$taskExit = 1
$taskTimedOut = $false
$taskCancelled = $false
try {
    $taskArgs = @('-batchmode', '-projectPath', ('"' + $Project + '"'), '-runTests', '-testPlatform', 'EditMode', '-testFilter', ('"' + $Filter + '"'), '-testResults', ('"' + (Join-Path $Output 'results.xml') + '"'), '-logFile', ('"' + (Join-Path $Output 'editor.log') + '"'))
    $taskProcess = Start-Process -FilePath $Unity -ArgumentList $taskArgs -PassThru -WindowStyle Hidden
    $taskDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $taskProcess.WaitForExit(500)) {
        if ([DateTime]::UtcNow -ge $taskDeadline -or (Test-Path -LiteralPath (Join-Path $Project 'Artifacts/GeneratedQA/NightShift/STOP'))) {
            Stop-Process -Id $taskProcess.Id -Force
            $taskProcess.WaitForExit()
            $taskCancelled = Test-Path -LiteralPath (Join-Path $Project 'Artifacts/GeneratedQA/NightShift/STOP')
            $taskTimedOut = -not $taskCancelled
            break
        }
    }
    $taskExit = if ($taskTimedOut) { 124 } elseif ($taskCancelled) { 125 } else { $taskProcess.ExitCode }
} finally {
    if ($null -ne $taskProcess -and -not $taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id -Force; $taskProcess.WaitForExit() }
    Restore-TaskPreferences
}
[pscustomobject]@{ completed = $true; preferencesRestored = $true; unityExitCode = $taskExit; timedOut = $taskTimedOut; cancelled = $taskCancelled } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'wrapper-outcome.json') -Encoding utf8
exit $taskExit
