param(
    [Parameter(Mandatory = $true)][string]$UnityEditorData,
    [Parameter(Mandatory = $true)][string[]]$ResponseFiles
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskOutput = Join-Path $taskRoot 'Artifacts/GeneratedQA/Phase6/ProductionBoundary'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskReport = [Collections.Generic.List[string]]::new()
Push-Location $taskRoot
try {
    foreach ($taskResponse in $ResponseFiles) {
        $taskInput = (Resolve-Path -LiteralPath $taskResponse).Path
        $taskConfiguration = Split-Path (Split-Path $taskInput) -Leaf
        $taskDestination = Join-Path $taskOutput $taskConfiguration
        New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
        $taskLines = [IO.File]::ReadAllLines($taskInput)
        $taskDevSources = @($taskLines | Where-Object { $_ -match '^"Assets[/\\]_Project[/\\]Dev[/\\].*\.cs"$' })
        if ($taskDevSources.Count -eq 0) { throw "No Dev source files found in $taskInput; refusing an unverified boundary check." }
        $taskRuntimeSources = @($taskLines | Where-Object { $_ -match '^"Assets[/\\]_Project[/\\]scripts[/\\].*\.cs"$' })
        if ($taskRuntimeSources.Count -eq 0) { throw "No Production source files found in $taskInput." }
        $taskFiltered = foreach ($taskLine in $taskLines) {
            if ($taskLine -match '^"Assets[/\\]_Project[/\\]Dev[/\\].*\.cs"$') { continue }
            if ($taskLine -match '^-out:') { '-out:"' + (Join-Path $taskDestination 'Production.dll') + '"'; continue }
            if ($taskLine -match '^-refout:') { '-refout:"' + (Join-Path $taskDestination 'Production.ref.dll') + '"'; continue }
            $taskLine
        }
        $taskResponseCopy = Join-Path $taskDestination 'Production.rsp'
        [IO.File]::WriteAllLines($taskResponseCopy, $taskFiltered, [Text.UTF8Encoding]::new($false))
        $taskLog = Join-Path $taskDestination 'compile.log'
        & (Join-Path $UnityEditorData 'NetCoreRuntime/dotnet.exe') exec (Join-Path $UnityEditorData 'DotNetSdkRoslyn/csc.dll') "@$taskResponseCopy" *> $taskLog
        $taskExit = $LASTEXITCODE
        $taskReport.Add("$taskConfiguration : exit=$taskExit; runtime sources=$($taskRuntimeSources.Count); excluded Dev sources=$($taskDevSources.Count)")
        if ($taskExit -ne 0) {
            Get-Content -LiteralPath $taskLog
            throw "Production compilation without Dev failed: $taskConfiguration"
        }
    }
    $taskReport.Add('PASS: Production compiles with every project Dev/Lab source removed.')
}
finally {
    [IO.File]::WriteAllLines((Join-Path $taskOutput 'results.txt'), $taskReport)
    Pop-Location
}
$taskReport
