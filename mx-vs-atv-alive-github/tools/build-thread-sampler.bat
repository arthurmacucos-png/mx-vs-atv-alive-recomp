@echo off
rem Builds the diagnostic thread sampler. Not part of the game build.
call "%~dp0dev-env.bat"
if errorlevel 1 exit /b 1
cd /d "%~dp0.."
clang++ -O2 -std=c++20 tools\thread-sampler.cpp -o out\thread-sampler.exe
if errorlevel 1 exit /b 1
echo THREAD_SAMPLER_BUILD_OK
