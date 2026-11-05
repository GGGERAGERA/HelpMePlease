$ErrorActionPreference = 'Stop'
# Exercise the detached launch branch without starting a real process. Windows
# can resolve multiple node applications; Start-Process requires one FilePath.
function Get-Command {
    [CmdletBinding()]
    param([string]$Name, [string]$CommandType)
    if ($Name -eq 'codex.exe') { return [pscustomobject]@{ Source = 'C:\codex runtime\codex.exe' } }
    if ($Name -ne 'node') { throw "Unexpected command lookup: $Name" }
    [pscustomobject]@{ Source = 'C:\first runtime\node.exe' }
    [pscustomobject]@{ Source = 'C:\second runtime\node.exe' }
}
function Start-Process {
    [CmdletBinding()]
    param([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory,
        [string]$WindowStyle, [switch]$PassThru, [string]$RedirectStandardOutput,
        [string]$RedirectStandardError)
    if ($FilePath -cne 'C:\first runtime\node.exe') { throw 'Must select exactly the first resolved node application.' }
    if ($WindowStyle -ne 'Hidden') { throw 'Detached launch must stay hidden.' }
    if ('night' -notin $ArgumentList) { throw 'Night mode argument missing.' }
    if ('--codex' -notin $ArgumentList -or '"C:\codex runtime\codex.exe"' -notin $ArgumentList) {
        throw 'Detached launch must pass the resolved native Codex executable.'
    }
    if (-not $RedirectStandardOutput -or -not $RedirectStandardError) { throw 'Detached launch needs both logs.' }
    $global:taskNightLaunchObserved = $true
    [pscustomobject]@{ Id = 12345 }
}
$global:taskNightLaunchObserved = $false
& (Join-Path $PSScriptRoot 'Start-NightShift.ps1') -Mode Night -Hours 1
if (-not $global:taskNightLaunchObserved) { throw 'Detached launch branch was not exercised.' }
Write-Output 'PASS: multiple node paths resolve to one hidden logged launch.'
