@echo off
rem Regenerate the recompiled code from mx_alive_manifest.toml and rebuild the
rem game. Used by the installer's update mode when the manifest changes (for
rem example new functions for the online mode). Only changed files recompile.
call "%~dp0dev-env.bat"
if errorlevel 1 exit /b 1

set "ROOT=%~dp0.."
set "SDK=%ROOT%\third_party\rexglue-sdk"
set "REXGLUE=%SDK%\out\install\win-amd64\bin\rexglue.exe"
set "OUT=%ROOT%\out\build\win-amd64-release"
cd /d "%ROOT%"

if not exist "%REXGLUE%" (
    echo rexglue.exe not found: "%REXGLUE%"
    exit /b 1
)

echo === regenerating recompiled code ===
"%REXGLUE%" codegen "mx_alive_manifest.toml"
if errorlevel 1 exit /b 1

echo === configuring mx_alive (win-amd64-release) ===
cmake --preset win-amd64-release -DCMAKE_EXE_LINKER_FLAGS=-loldnames
if not errorlevel 1 goto :build

rem The build folder was created at another path (project folder moved):
rem drop the CMake cache and configure again. This recompiles everything.
echo === stale CMake cache, configuring from scratch ===
if exist "%OUT%\CMakeCache.txt" del /q "%OUT%\CMakeCache.txt"
if exist "%OUT%\CMakeFiles" rmdir /s /q "%OUT%\CMakeFiles"
if exist "%OUT%\build.ninja" del /q "%OUT%\build.ninja"
if exist "%OUT%\.ninja_deps" del /q "%OUT%\.ninja_deps"
if exist "%OUT%\.ninja_log" del /q "%OUT%\.ninja_log"
cmake --preset win-amd64-release -DCMAKE_EXE_LINKER_FLAGS=-loldnames
if errorlevel 1 exit /b 1

:build
echo === building mx_alive ===
cmake --build --preset win-amd64-release
if errorlevel 1 exit /b 1
echo GAME_BUILD_OK
