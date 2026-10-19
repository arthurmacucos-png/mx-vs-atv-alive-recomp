@echo off
rem 2560x1440 output. The guest still sees a 1280x720 video mode, but the GPU
rem renders draw calls at 2x on both axes: 1280x720 * 2 = 2560x1440, a 1:1
rem match for a 1440p monitor. Fullscreen (default) uses the monitor mode.
rem   usage: run-game-2k.bat [fps]      (default 120)
setlocal
set "FPS=%~1"
if "%FPS%"=="" set "FPS=120"
echo WARNING: 2x draw-resolution scaling shows visual artifacts in this title
echo and has crashed mid-save during testing. A timestamped save backup is made
echo before launch. Press Ctrl+C to cancel.
timeout /t 5 >nul
set "MX_ALIVE_EXTRA=--resolution_scale=2"
call "%~dp0run-game.bat" %FPS%
