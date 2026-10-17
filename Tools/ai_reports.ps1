# Runs the AI studies headless in the mirrored copy the web build uses (..\SlayTheClone2025-WebBuild), so the editor stays
# free for playing (the user: long AI runs block manual testing): the balance report (Debug > AI Balance Report) and the
# shuffled tournament (Debug > AI Shuffled Tournament), then copies their reports and scores back into the project.
#   powershell -ExecutionPolicy Bypass -File Tools/ai_reports.ps1            both studies
#   powershell -ExecutionPolicy Bypass -File Tools/ai_reports.ps1 -Only Balance   (or -Only Shuffled)
param(
    [ValidateSet("Both", "Balance", "Shuffled")] [string]$Only = "Both",
    [string]$UnityVersion = "6000.3.26f1"
)
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$copy = Join-Path (Split-Path -Parent $project) "SlayTheClone2025-WebBuild"
$log = Join-Path $project "Logs\ai-reports.log"
$unity = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $UnityVersion isn't installed at $unity" }
New-Item -ItemType Directory -Force -Path $copy, (Split-Path $log) | Out-Null
foreach ($folder in "Assets", "Packages", "ProjectSettings") {
    robocopy (Join-Path $project $folder) (Join-Path $copy $folder) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $folder failed (robocopy $LASTEXITCODE)" }
}

$studies = @()
if ($Only -ne "Shuffled") { $studies += @{ Method = "Zeitgeist.AITournamentMenu.RunBalance"; Files = @("Assets\Documentation\Balance Report.txt", "Assets\Resources\CardScores.json") } }
if ($Only -ne "Balance") { $studies += @{ Method = "Zeitgeist.AITournamentMenu.RunShuffled"; Files = @("Assets\Documentation\Shuffled Tournament.txt", "Assets\Resources\ShuffledScores.json") } }
foreach ($study in $studies) {
    $started = Get-Date
    Write-Host "Running $($study.Method) in $copy..."
    $process = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "`"$copy`"", "-executeMethod", $study.Method, "-logFile", "`"$log`"")
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    if ($process.ExitCode -ne 0) { throw "$($study.Method) failed after $minutes minutes (exit code $($process.ExitCode)): see $log" }
    foreach ($file in $study.Files) {
        $from = Join-Path $copy $file
        if (Test-Path $from) { Copy-Item $from (Join-Path $project $file) -Force }
    }
    Write-Host "$($study.Method) done in $minutes minutes; copied $($study.Files -join ', ')"
}
