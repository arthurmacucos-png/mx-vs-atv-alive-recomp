@echo off
rem Debug launch with the same frame-rate selection as run-game.bat.
rem   usage: run-game-debug.bat [fps]   (default 60)
setlocal
set "FPS=%~1"
if "%FPS%"=="" set "FPS=60"
set /a REFRESH=%FPS%*2
set "ROOT=%~dp0.."
set "EXE=%ROOT%\out\build\win-amd64-release\mx_alive.exe"
set "GAME=%ROOT%\Game\MX vs. ATV Alive (USA Europe)"
if not exist "%EXE%" (
    echo Build not found: %EXE%
    echo Run tools\build-game.bat first.
    exit /b 1
)
call "%~dp0controller-env.bat"
start "" "%EXE%" --game_data_root "%GAME%" --log_level debug --mnk_mode %CONTROLLER_ARGS% ^
    --gpu_allow_invalid_fetch_constants --d3d12_readback_resolve ^
    --query_occlusion_fake_sample_count=-1 --video_mode_refresh_rate %REFRESH%
