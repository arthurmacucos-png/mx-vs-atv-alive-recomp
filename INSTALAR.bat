@echo off
rem Instalação em um clique do MX vs. ATV Alive (port nativo).
rem Você só precisa da SUA cópia do jogo de Xbox 360.
title MX vs. ATV Alive - instalacao
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\setup.ps1"
echo.
pause
