<#
The browser build, made from a separate copy of the project so the Unity editor in use stays free (the user, 2026-10-17).

  1. Mirrors Assets, Packages and ProjectSettings (uncommitted changes included) into ..\SlayTheClone2025-WebBuild.
     The copy keeps its own Library: the first build imports everything (slow), later ones only what changed.
  2. Builds it for WebGL with a Unity that has no window (SlayTheClone.WebBuild.Build), into Builds\WebGL here.
  3. With -Serve, serves Builds\WebGL at http://localhost:<Port> (Ctrl+C stops it). -ServeOnly serves without building.

Usage (PowerShell, from the project folder):
  .\Tools\web_build.ps1 -Serve
  .\Tools\web_build.ps1 -ServeOnly -Port 8080
The build's log: Logs\web-build.log.
#>
param(
    [switch]$Serve,
    [switch]$ServeOnly,
    [int]$Port = 8080,
    [string]$UnityVersion = "6000.3.26f1"
)
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$copy = Join-Path (Split-Path -Parent $project) "SlayTheClone2025-WebBuild"
$output = Join-Path $project "Builds\WebGL"
$log = Join-Path $project "Logs\web-build.log"
$unity = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"

if (-not $ServeOnly) {
    if (-not (Test-Path $unity)) { throw "Unity $UnityVersion isn't installed at $unity" }
    New-Item -ItemType Directory -Force -Path $copy, (Split-Path $log) | Out-Null
    foreach ($folder in "Assets", "Packages", "ProjectSettings") {
        # /MIR: the copy matches the project exactly; robocopy's codes below 8 are successes
        robocopy (Join-Path $project $folder) (Join-Path $copy $folder) /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copying $folder failed (robocopy $LASTEXITCODE)" }
    }
    Write-Host "Building for the browser from $copy (the first time imports the whole project: it takes a while)..."
    $started = Get-Date
    # First run: the define URP's Compatibility Mode needs to build (see WebBuild.Prepare); the build run compiles with it
    $prepare = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "`"$copy`"", "-buildTarget", "WebGL",
        "-executeMethod", "SlayTheClone.WebBuild.Prepare", "-logFile", "`"$log`"")
    if ($prepare.ExitCode -ne 0) { throw "Preparing the copy failed (exit code $($prepare.ExitCode)): see $log" }
    $process = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "`"$copy`"", "-buildTarget", "WebGL",
        "-executeMethod", "SlayTheClone.WebBuild.Build", "-out", "`"$output`"", "-logFile", "`"$log`"")
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    if ($process.ExitCode -ne 0) { throw "The build failed after $minutes minutes (exit code $($process.ExitCode)): see $log" }
    Write-Host "Built in $minutes minutes: $output"
}

if ($Serve -or $ServeOnly) {
    if (-not (Test-Path (Join-Path $output "index.html"))) { throw "No build in $output yet: run without -ServeOnly first" }
    Write-Host "Serving the game at http://localhost:$Port (Ctrl+C stops it)"
    python -m http.server $Port --bind 127.0.0.1 --directory $output
}
