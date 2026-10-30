param(
    [ValidateSet('Night', 'Smoke', 'Status', 'Stop')][string]$Mode = 'Status',
    [ValidateRange(0.01, 7)][double]$Hours = 6,
    [string]$DeadlineAt,
    [string]$ResumeFrom,
    [string]$Unity = 'G:/unity/editor/6000.3.13f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$taskProject = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskBase = Join-Path $taskProject 'Artifacts/GeneratedQA/NightShift'
New-Item -ItemType Directory -Path $taskBase -Force | Out-Null
if ($Mode -eq 'Stop') {
    Set-Content -LiteralPath (Join-Path $taskBase 'STOP') -Value 'User requested stop' -Encoding utf8
    Write-Output 'STOP requested. Unity will stop and restore PlayerPrefs before the controller exits.'
    exit 0
}
if ($Mode -eq 'Status') {
    $taskLatest = Join-Path $taskBase 'latest-session.json'
    if (-not (Test-Path -LiteralPath $taskLatest)) { Write-Output 'No Night Shift session yet.'; exit 0 }
    $taskSession = Get-Content -LiteralPath $taskLatest -Raw | ConvertFrom-Json
    $taskLive = Get-Process -Id $taskSession.pid -ErrorAction SilentlyContinue
    Write-Output "PID=$($taskSession.pid); alive=$($null -ne $taskLive); mode=$($taskSession.mode); session=$($taskSession.session)"
    Get-Content -LiteralPath (Join-Path $taskSession.session 'session.log') -Tail 8 -ErrorAction SilentlyContinue
    $taskHeartbeat = Join-Path $taskSession.session 'heartbeat.json'
    if (Test-Path -LiteralPath $taskHeartbeat) { Get-Content -LiteralPath $taskHeartbeat -Raw }
    exit 0
}
if (Test-Path -LiteralPath (Join-Path $taskBase 'session.lock')) { throw 'A session lock exists. Use Status; do not start a second controller.' }
if (Test-Path -LiteralPath (Join-Path $taskBase 'STOP')) { throw 'STOP exists. Remove that exact file deliberately before a new run.' }
$taskNode = (Get-Command node -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$taskScript = Join-Path $PSScriptRoot 'night-shift.mjs'
if ($Mode -eq 'Smoke') {
    & $taskNode $taskScript --mode smoke --hours $Hours --unity $Unity
    exit $LASTEXITCODE
}
$taskStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$taskArgs = @(('"' + $taskScript + '"'), '--mode', 'night', '--hours', $Hours.ToString([Globalization.CultureInfo]::InvariantCulture), '--unity', ('"' + $Unity + '"'))
if ($DeadlineAt) { $taskArgs += @('--deadline-at', ('"' + $DeadlineAt + '"')) }
if ($ResumeFrom) { $taskArgs += @('--resume-from', ('"' + $ResumeFrom + '"')) }
$taskProcess = Start-Process -FilePath $taskNode -ArgumentList $taskArgs -WorkingDirectory $taskProject -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskBase "launcher-$taskStamp.stdout.log") -RedirectStandardError (Join-Path $taskBase "launcher-$taskStamp.stderr.log")
Write-Output "Started local Night Shift PID=$($taskProcess.Id); maximum $Hours hours. Use -Mode Status or -Mode Stop."
