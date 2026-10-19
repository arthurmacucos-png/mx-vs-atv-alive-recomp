@echo off
rem Build and install the pinned rexglue-sdk, applying this project's local
rem patches first. Run this once after cloning, and again after changing the
rem submodule or the patch file.
call "%~dp0dev-env.bat"
if errorlevel 1 exit /b 1

set "ROOT=%~dp0.."
set "SDK=%ROOT%\third_party\rexglue-sdk"
set "PATCH=%ROOT%\patches\rexglue-local.patch"

if not exist "%SDK%\CMakeLists.txt" (
    echo rexglue-sdk submodule is missing.
    echo Run: git submodule update --init --recursive
    exit /b 1
)

echo === materializing libmspack symlinks ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0fix-rexglue-symlinks.ps1" -Sdk "%SDK%"
if errorlevel 1 exit /b 1

rem The local patch adds the async readback copy worker, guest/host FPS
rem telemetry, crash RIP diagnostics, the level-load log and the refresh clamp
rem used by the high-FPS presets. Apply it only if it is not already present.
git -C "%SDK%" apply --check "%PATCH%" >nul 2>&1
if not errorlevel 1 (
    echo === applying local SDK patches ===
    git -C "%SDK%" apply "%PATCH%"
    if errorlevel 1 exit /b 1
) else (
    git -C "%SDK%" apply --check --reverse "%PATCH%" >nul 2>&1
    if errorlevel 1 (
        echo WARNING: patches\rexglue-local.patch is neither applicable nor already applied.
        echo The submodule has other local changes; building it as-is.
    ) else (
        echo === local SDK patches already applied ===
    )
)

cd /d "%SDK%"
echo === configuring rexglue (win-amd64) ===
cmake --preset win-amd64
if errorlevel 1 exit /b 1
echo === building rexglue Release + install ===
cmake --build out\build\win-amd64 --config Release --target install
if errorlevel 1 exit /b 1
echo REXGLUE_BUILD_OK
