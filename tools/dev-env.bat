@echo off
rem Initialize the MSVC x64 environment used by Clang for Windows builds.
rem rexglue compiles with Clang but needs the MSVC C++ standard library headers
rem and the Windows SDK that ship with Visual Studio's C++ workload.
rem
rem Labels are used instead of parenthesised blocks because "%ProgramFiles(x86)%"
rem contains parentheses, which confuse cmd's block parser.

set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "VSPATH="
if not exist "%VSWHERE%" goto :fallback
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if defined VSPATH goto :have_vs

:fallback
rem Fallback for the toolchain this project was developed on.
set "FALLBACK=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%FALLBACK%" goto :missing
call "%FALLBACK%" >nul
exit /b %errorlevel%

:have_vs
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
exit /b %errorlevel%

:missing
echo ERROR: Visual Studio C++ build tools (MSVC x64) not found.
echo Install Visual Studio 2022 Build Tools with the "Desktop development with C++"
echo workload, then retry.
exit /b 1
