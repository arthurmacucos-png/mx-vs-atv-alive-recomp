param([Parameter(Mandatory = $true)][string]$Sdk)

# Git for Windows checks out symlinks as small text files containing the
# relative target path. libmspack's cabextract directory relies on such
# symlinks, so materialize them as real files before building.
$roots = @(
    (Join-Path $Sdk 'thirdparty\libmspack\cabextract\mspack')
)

foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -File | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $text) { return }
        $rel = $text.Trim()
        if ($rel.StartsWith('../') -or $rel.StartsWith('..\')) {
            $target = [System.IO.Path]::GetFullPath((Join-Path $_.DirectoryName $rel))
            if (Test-Path -LiteralPath $target -PathType Leaf) {
                Copy-Item -LiteralPath $target -Destination $_.FullName -Force
                Write-Host "fixed $($_.Name)"
            } else {
                Write-Warning "missing target for $($_.Name): $target"
            }
        }
    }
}
