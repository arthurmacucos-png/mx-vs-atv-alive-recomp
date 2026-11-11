# One-click setup: installs the toolchain, fetches rexglue, copies YOUR game
# files, builds everything and creates a desktop shortcut to the launcher.
# Run through INSTALAR.bat. Windows PowerShell 5.1 compatible.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$Root = Split-Path -Parent $PSScriptRoot
$GameDir = Join-Path $Root 'Game\MX vs. ATV Alive (USA Europe)'
$Sdk = Join-Path $Root 'third_party\rexglue-sdk'
$SdkCommit = 'f5337cdc947ff6d4c4196737e2c807a48f2a1fc2'   # rexglue-sdk v0.10.0
Set-Location $Root

function Step($text) { Write-Host ''; Write-Host "=== $text ===" -ForegroundColor Yellow }
function Fail($text) {
    Write-Host ''; Write-Host "ERRO: $text" -ForegroundColor Red
    [System.Windows.Forms.MessageBox]::Show($text, 'MX vs. ATV Alive - instalação', 'OK', 'Error') | Out-Null
    exit 1
}
function Refresh-Path {
    $env:Path = 'C:\Program Files\LLVM\bin;' +
        [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
        [Environment]::GetEnvironmentVariable('Path', 'User')
}
function Have($cmd) { Refresh-Path; return [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function Winget($id, $extra) {
    Write-Host "Instalando $id ..."
    $wargs = @('install', '--id', $id, '-e', '--accept-package-agreements', '--accept-source-agreements')
    if ($extra) { $wargs += $extra } else { $wargs += '--silent' }
    & winget @wargs
}

Write-Host 'MX vs. ATV Alive - port nativo para PC' -ForegroundColor Cyan
Write-Host 'Este instalador NÃO baixa o jogo: você vai escolher a pasta da SUA cópia de Xbox 360.'

# 1. Game files first, so nothing heavy is installed for nothing.
Step '1/6 Arquivos do jogo'
$xex = Join-Path $GameDir 'default.xex'
if (-not (Test-Path $xex)) {
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Escolha a pasta do MX vs. ATV Alive de Xbox 360 (a que tem o default.xex)'
    if ($dlg.ShowDialog() -ne 'OK') { Fail 'Nenhuma pasta escolhida.' }
    $src = $dlg.SelectedPath
    if (-not (Test-Path (Join-Path $src 'default.xex'))) { Fail "Não achei default.xex em: $src" }
    # Check the exact build the recompilation was made for.
    $b = [System.IO.File]::ReadAllBytes((Join-Path $src 'default.xex'))
    function BE32($o) { return [uint32]((([uint64]$b[$o]) -shl 24) -bor (([uint64]$b[$o+1]) -shl 16) -bor (([uint64]$b[$o+2]) -shl 8) -bor [uint64]$b[$o+3]) }
    if ([Text.Encoding]::ASCII.GetString($b, 0, 4) -ne 'XEX2') { Fail 'default.xex inválido.' }
    $ok = $false
    for ($i = 0; $i -lt (BE32 0x14); $i++) {
        if ((BE32 (0x18 + $i * 8)) -eq 0x00040006) {
            $o = [int](BE32 (0x1C + $i * 8))
            $media = '{0:X8}' -f (BE32 $o); $title = '{0:X8}' -f (BE32 ($o + 12))
            Write-Host "Title $title  Media $media"
            $ok = ($title -eq '5451087B' -and $media -eq '5306E1D5')
        }
    }
    if (-not $ok) { Fail 'Esta cópia não é a versão suportada (Xbox 360 USA/Europa, Title 5451087B, Media 5306E1D5).' }
    Write-Host 'Copiando os arquivos do jogo (cerca de 2,7 GB)...'
    New-Item -ItemType Directory -Force $GameDir | Out-Null
    robocopy $src $GameDir /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Fail 'Falha ao copiar os arquivos do jogo.' }
} else { Write-Host 'Arquivos do jogo já estão no lugar.' }

# 2. Toolchain.
Step '2/6 Ferramentas de compilação'
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { Fail 'winget não encontrado. Atualize o "Instalador de Aplicativo" pela Microsoft Store.' }
if (-not (Have git))   { Winget 'Git.Git' }
if (-not (Have cmake)) { Winget 'Kitware.CMake' }
if (-not (Have ninja)) { Winget 'Ninja-build.Ninja' }
if (-not (Have clang)) { Winget 'LLVM.LLVM' }
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = $null
if (Test-Path $vswhere) { $vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath }
if (-not $vs) {
    Write-Host 'Instalando Visual Studio 2022 Build Tools (C++). Pode demorar e pedir permissão de administrador.'
    Winget 'Microsoft.VisualStudio.2022.BuildTools' @('--override', '--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended')
}
Refresh-Path
foreach ($c in 'git', 'cmake', 'ninja', 'clang') { if (-not (Have $c)) { Fail "$c não ficou disponível. Reinicie o PC e rode o INSTALAR.bat de novo." } }

# 3. rexglue-sdk (works for both "git clone" and "Download ZIP").
Step '3/6 rexglue-sdk'
if (-not (Test-Path (Join-Path $Sdk 'CMakeLists.txt'))) {
    if (Test-Path (Join-Path $Root '.git')) {
        git submodule update --init --recursive
    } else {
        if (Test-Path $Sdk) { Remove-Item $Sdk -Recurse -Force }
        git clone https://github.com/rexglue/rexglue-sdk.git $Sdk
        git -C $Sdk checkout $SdkCommit
        git -C $Sdk submodule update --init --recursive
    }
    if ($LASTEXITCODE -ne 0) { Fail 'Falha ao baixar o rexglue-sdk.' }
} else { Write-Host 'rexglue-sdk já presente.' }

# 4-5. Build.
Step '4/6 Compilando o rexglue (com a correção da água)'
cmd /c "`"$Root\tools\build-rexglue.bat`""
if ($LASTEXITCODE -ne 0) { Fail 'Falha ao compilar o rexglue. Veja as mensagens acima.' }

Step '5/6 Recompilando o jogo (a primeira vez demora de 30 a 60 minutos)'
cmd /c "`"$Root\tools\build-game.bat`""
if ($LASTEXITCODE -ne 0) { Fail 'Falha ao compilar o jogo. Veja as mensagens acima.' }

# 6. Launcher + shortcut.
Step '6/6 Launcher e atalho'
$launcher = Join-Path $Root 'MX Alive Launcher.exe'
if (-not (Test-Path $launcher)) {
    cmd /c "`"$Root\launcher\build-launcher.bat`""
}
$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MX vs ATV Alive.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk)
$s.TargetPath = $launcher; $s.WorkingDirectory = $Root; $s.IconLocation = "$launcher,0"; $s.Save()

Write-Host ''
Write-Host 'PRONTO! Atalho "MX vs ATV Alive" criado na Área de Trabalho.' -ForegroundColor Green
Write-Host 'Tem DLCs da sua conta? Use o botão "Instalar DLC..." no launcher.'
Start-Process $launcher -WorkingDirectory $Root
