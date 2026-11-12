@echo off
rem MX vs. ATV Alive - abre o instalador.
rem Baixa o codigo do instalador e compila NESTE PC com o compilador que ja vem
rem no Windows. Um .exe baixado pronto, sem assinatura digital, costuma ser
rem apagado pelo antivirus; compilado aqui, isso nao acontece.
title MX vs. ATV Alive - instalador
echo Preparando o instalador do MX vs. ATV Alive...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
 "$ErrorActionPreference = 'Stop';" ^
 "[Net.ServicePointManager]::SecurityProtocol = 'Tls12';" ^
 "$t = Join-Path $env:LOCALAPPDATA 'MXAliveSetup';" ^
 "New-Item -ItemType Directory -Force $t | Out-Null;" ^
 "$u = 'https://raw.githubusercontent.com/arthurmacucos-png/mx-vs-atv-alive-recomp/main/launcher/';" ^
 "$src = Join-Path $t 'setup.cs'; $ico = Join-Path $t 'icon.ico'; $out = Join-Path $t 'MX Alive Setup.exe';" ^
 "Invoke-WebRequest ($u + 'MxAliveSetup.cs') -OutFile $src -UseBasicParsing;" ^
 "Invoke-WebRequest ($u + 'icon.ico') -OutFile $ico -UseBasicParsing;" ^
 "$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';" ^
 "if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' };" ^
 "if (Test-Path $out) { Remove-Item $out -Force };" ^
 "$refs = 'System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll' | ForEach-Object { '/r:' + $_ };" ^
 "& $csc /nologo /target:winexe /optimize+ ('/win32icon:' + $ico) ('/out:' + $out) $refs $src;" ^
 "if (-not (Test-Path $out)) { throw 'Falha ao compilar o instalador.' };" ^
 "Start-Process $out"
if errorlevel 1 (
    echo.
    echo Nao deu certo. Confira a internet e tente de novo.
    pause
)
