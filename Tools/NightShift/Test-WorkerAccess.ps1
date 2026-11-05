param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$TargetsFile,
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($Project).TrimEnd([IO.Path]::DirectorySeparatorChar)
$taskChecked = @()
$taskDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$taskNewDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$taskPassed = $false
$taskError = $null
try {
    foreach ($taskRelative in @(Get-Content -LiteralPath $TargetsFile -Raw | ConvertFrom-Json)) {
        if ([string]::IsNullOrWhiteSpace($taskRelative) -or [IO.Path]::IsPathRooted($taskRelative)) {
            throw 'Write target must be a project-relative file path.'
        }
        $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
        if (-not $taskTarget.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Write target escapes the project.'
        }
        $taskParts = [IO.Path]::GetRelativePath($taskRoot, $taskTarget) -split '[/\\]'
        if ($taskParts[0] -in '.git', '.codex', '.agents', '.superpowers', 'Packages' -or
            ($taskParts[0] -eq 'Tools' -and $taskParts[1] -eq 'NightShift')) {
            throw 'Write target is protected by Night Shift policy.'
        }
        $taskParent = Split-Path -Parent $taskTarget
        $taskMissingParent = $false
        while (-not (Test-Path -LiteralPath $taskParent -PathType Container)) {
            if (Test-Path -LiteralPath $taskParent) { throw 'A parent component is already a file.' }
            $taskMissingParent = $true
            $taskParent = Split-Path -Parent $taskParent
        }
        $taskAncestor = $taskParent
        while ($taskAncestor -ne $taskRoot) {
            if ((Get-Item -LiteralPath $taskAncestor).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Write target passes through a reparse point.'
            }
            $taskAncestor = Split-Path -Parent $taskAncestor
        }
        if (Test-Path -LiteralPath $taskTarget) {
            $taskItem = Get-Item -LiteralPath $taskTarget
            if ($taskItem.PSIsContainer -or ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Write target must be a regular file.'
            }
            $taskHandle = [IO.File]::Open($taskTarget, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite,
                [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
            $taskHandle.Dispose()
        }
        if ($taskDirectories.Add($taskParent)) {
            $taskScratch = Join-Path $taskParent ('.night-shift-write-' + [guid]::NewGuid().ToString('N') + '.tmp')
            $taskHandle = $null
            try {
                $taskHandle = [IO.File]::Open($taskScratch, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
                $taskHandle.WriteByte(42)
            } finally {
                if ($taskHandle) {
                    $taskHandle.Dispose()
                    Remove-Item -LiteralPath $taskScratch -ErrorAction Stop
                }
            }
        }
        if ($taskMissingParent -and $taskNewDirectories.Add($taskParent)) {
            $taskScratchDirectory = Join-Path $taskParent ('.night-shift-directory-' + [guid]::NewGuid().ToString('N'))
            $taskNestedDirectory = Join-Path $taskScratchDirectory 'nested'
            $taskNestedFile = Join-Path $taskNestedDirectory 'write.tmp'
            try {
                [IO.Directory]::CreateDirectory($taskNestedDirectory) | Out-Null
                [IO.File]::WriteAllBytes($taskNestedFile, [byte[]]@(42))
            } finally {
                if (Test-Path -LiteralPath $taskNestedFile) { Remove-Item -LiteralPath $taskNestedFile -ErrorAction Stop }
                if (Test-Path -LiteralPath $taskNestedDirectory) { Remove-Item -LiteralPath $taskNestedDirectory -ErrorAction Stop }
                if (Test-Path -LiteralPath $taskScratchDirectory) { Remove-Item -LiteralPath $taskScratchDirectory -ErrorAction Stop }
            }
        }
        $taskChecked += [string]$taskRelative
    }
    $taskPassed = $true
} catch { $taskError = $_.Exception.Message }
[pscustomobject]@{ passed = $taskPassed; targets = @($taskChecked); error = $taskError } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Output -Encoding utf8
if (-not $taskPassed) { exit 1 }
