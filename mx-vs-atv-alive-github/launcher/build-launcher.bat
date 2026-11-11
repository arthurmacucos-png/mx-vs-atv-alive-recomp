@echo off
rem Builds "MX Alive Launcher.exe" in the repository root with the C# compiler
rem that ships with Windows (.NET Framework 4.x). Nothing to install.
setlocal
set "HERE=%~dp0"
set "ROOT=%HERE%.."
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo csc.exe not found. .NET Framework 4.x is required.
    exit /b 1
)
if not exist "%HERE%icon.ico" powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%make-icon.ps1"
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
    /win32icon:"%HERE%icon.ico" ^
    /out:"%ROOT%\MX Alive Launcher.exe" ^
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "%HERE%MxAliveLauncher.cs"
if errorlevel 1 exit /b 1
echo LAUNCHER_BUILD_OK
