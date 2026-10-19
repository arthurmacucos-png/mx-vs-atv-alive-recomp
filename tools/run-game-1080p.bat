@echo off
rem 1920x1080 guest video mode (a standard Xbox 360 mode). Sharper than the
rem 720p baseline without draw-resolution scaling, but resolve readback volume
rem grows with resolution, so the default is the supported 60 fps.
rem   usage: run-game-1080p.bat [fps]      (default 60)
setlocal
set "FPS=%~1"
if "%FPS%"=="" set "FPS=60"
set "MX_ALIVE_EXTRA=--video_mode_width=1920 --video_mode_height=1080"
call "%~dp0run-game.bat" %FPS%
