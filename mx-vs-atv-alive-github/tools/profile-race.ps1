# Samples per-thread CPU usage of the game while it runs.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\profile-race.ps1
#   optional: -Seconds 180 -ProcessName mx_alive -OutFile out\race-cpu.csv
#
# Waits for the game process to appear, then writes one CSV row per thread per
# second (plus a process-total row). Join timestamps against the `guest VdSwap`
# lines in out\build\win-amd64-release\logs\mx_alive_*.log to isolate the race.
param(
    [string]$ProcessName = "mx_alive",
    [int]$Seconds = 180,
    [int]$WaitSeconds = 300,
    [string]$OutFile = "out\race-cpu.csv"
)

$ErrorActionPreference = "Stop"
$startWait = Get-Date
$proc = $null
while (-not $proc -and ((Get-Date) - $startWait).TotalSeconds -lt $WaitSeconds) {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { Start-Sleep -Milliseconds 500 }
}
if (-not $proc) { Write-Error "Process '$ProcessName' did not start within $WaitSeconds s"; exit 1 }

Write-Host "Sampling PID $($proc.Id) for $Seconds s -> $OutFile"
$outDir = Split-Path -Parent $OutFile
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$rows = [System.Collections.Generic.List[string]]::new()
$rows.Add("time,pid,tid,name,cpu_percent,state,wait_reason")

$deadline = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $deadline) {
    $proc = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if (-not $proc) { Write-Host "Process exited"; break }
    $now = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

    $procRow = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter "IDProcess=$($proc.Id)" -ErrorAction SilentlyContinue
    $procCpu = if ($procRow) { $procRow.PercentProcessorTime } else { "" }
    $rows.Add("$now,$($proc.Id),0,process,$procCpu,,")

    $threads = Get-CimInstance Win32_PerfFormattedData_PerfProc_Thread -Filter "IDProcess=$($proc.Id)" -ErrorAction SilentlyContinue
    foreach ($t in $threads) {
        $name = ($t.Name -replace '^[^/]*/', '')
        $rows.Add("$now,$($proc.Id),$($t.IDThread),$name,$($t.PercentProcessorTime),$($t.ThreadState),$($t.ThreadWaitReason)")
    }
    Start-Sleep -Seconds 1
}

Set-Content -Path $OutFile -Value $rows
Write-Host "Wrote $($rows.Count - 1) rows to $OutFile"

# Quick summary: mean CPU per thread while the process was alive.
$data = Import-Csv $OutFile
$threadRows = $data | Where-Object { $_.tid -ne "0" -and $_.cpu_percent -ne "" }
$summary = $threadRows | Group-Object tid, name | ForEach-Object {
    $cpu = $_.Group | Measure-Object -Property cpu_percent -Average -Maximum
    [pscustomobject]@{
        tid = $_.Group[0].tid
        name = $_.Group[0].name
        avg_cpu = [math]::Round($cpu.Average, 1)
        max_cpu = [math]::Round($cpu.Maximum, 1)
    }
} | Sort-Object avg_cpu -Descending | Select-Object -First 15
$summary | Format-Table -AutoSize
$procRows = $data | Where-Object { $_.tid -eq "0" -and $_.cpu_percent -ne "" }
$procSummary = $procRows | Measure-Object -Property cpu_percent -Average -Maximum
Write-Host ("Process total CPU: avg {0:N1}%, max {1:N1}% (100% per logical core; {2} logical cores)" -f `
    $procSummary.Average, $procSummary.Maximum, [Environment]::ProcessorCount)
