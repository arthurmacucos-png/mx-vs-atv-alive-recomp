# Creates a timestamped backup of the game's save data before launching.
#
# Backs up everything under the save root except the shader cache, so profile
# saves and achievements are preserved. Keeps the newest 20 automatic backups.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\backup-save.ps1
param(
    # The real Documents folder (it is redirected when OneDrive backs it up).
    [string]$SaveRoot = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) "mx_alive"),
    [int]$Keep = 20
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SaveRoot)) {
    exit 0
}

$parent = Split-Path $SaveRoot -Parent
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dest = Join-Path $parent "mx_alive.bak-$stamp"

New-Item -ItemType Directory -Path $dest | Out-Null
try {
    Get-ChildItem $SaveRoot | Where-Object { $_.Name -ne "cache" } | ForEach-Object {
        Copy-Item $_.FullName -Destination $dest -Recurse -Force
    }
} catch {
    # A failed backup must not prevent the game from starting.
    Write-Warning "Save backup failed: $_"
    exit 0
}

# Prune only automatic timestamped backups.
Get-ChildItem $parent -Directory -Filter "mx_alive.bak-*" |
    Where-Object { $_.Name -match "^mx_alive\.bak-\d{8}-\d{6}$" } |
    Sort-Object Name -Descending |
    Select-Object -Skip $Keep |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
