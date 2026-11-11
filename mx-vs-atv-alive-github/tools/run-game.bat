@echo off
rem Launch the native build.
rem   usage: run-game.bat [fps]      (default 60)
rem   fps: 30 = Console, 60, 120, 144, 240
rem MX vs ATV Alive waits two guest vblanks per frame, so the guest refresh
rem rate is set to twice the requested frame rate.
setlocal
set "FPS=%~1"
if "%FPS%"=="" set "FPS=60"
rem Extra flags come from MX_ALIVE_EXTRA because cmd.exe splits batch
rem parameters on '=', which would mangle --name=value forms.
set "EXTRA=%MX_ALIVE_EXTRA%"
set /a REFRESH=%FPS%*2
set "ROOT=%~dp0.."
set "EXE=%ROOT%\out\build\win-amd64-release\mx_alive.exe"
set "GAME=%ROOT%\Game\MX vs. ATV Alive (USA Europe)"
if not exist "%EXE%" (
    echo Build not found: %EXE%
    echo Run tools\build-game.bat first.
    exit /b 1
)
rem Snapshot the save before every launch; a crash mid-save has truncated it.
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\tools\backup-save.ps1" >nul 2>&1
call "%~dp0controller-env.bat"
start "" "%EXE%" --game_data_root "%GAME%" --log_level info --mnk_mode %CONTROLLER_ARGS% ^
    --gpu_allow_invalid_fetch_constants --d3d12_readback_resolve ^
    --query_occlusion_fake_sample_count=-1 --video_mode_refresh_rate %REFRESH% %EXTRA%
