@echo off
rem Generate recompiled code (first run), configure and build the native game.
call "%~dp0dev-env.bat"
if errorlevel 1 exit /b 1

set "ROOT=%~dp0.."
set "SDK=%ROOT%\third_party\rexglue-sdk"
set "REXGLUE=%SDK%\out\install\win-amd64\bin\rexglue.exe"
set "GAME=%ROOT%\Game\MX vs. ATV Alive (USA Europe)"
cd /d "%ROOT%"

if not exist "%GAME%\default.xex" (
    echo Game data not found: "%GAME%\default.xex"
    echo Copy your own Xbox 360 game files into:
    echo   "%GAME%"
    echo See docs\BUILDING.md for the expected layout.
    exit /b 1
)

if not exist "%REXGLUE%" (
    echo rexglue.exe not found: "%REXGLUE%"
    echo Run tools\build-rexglue.bat first.
    exit /b 1
)

rem The first CMake configure needs generated/default/sources.cmake to exist or
rem it cannot create the recompiled-object target. Later builds regenerate the
rem code through the codegen dependency in generated/rexglue.cmake.
if not exist "generated\default\sources.cmake" (
    echo === generating recompiled code (first run) ===
    "%REXGLUE%" codegen "mx_alive_manifest.toml"
    if errorlevel 1 exit /b 1
)

echo === configuring mx_alive (win-amd64-release) ===
cmake --preset win-amd64-release
if errorlevel 1 exit /b 1
echo === building mx_alive ===
cmake --build --preset win-amd64-release
if errorlevel 1 exit /b 1
echo GAME_BUILD_OK
