@echo off
rem Controller setup shared by the launchers (call it after setlocal).
rem Forces the SDL3 input backend so PS5 (DualSense), PS4, Switch Pro and
rem generic USB/Bluetooth pads work, not only XInput (Xbox) controllers.
rem Sets CONTROLLER_ARGS for the mx_alive.exe command line (always --name=value:
rem cvars registered after startup ignore the "--name value" form).

set "CONTROLLER_DB=%~dp0..\controllers\gamecontrollerdb.txt"

rem SDL3 reads hints from environment variables. HIDAPI gives native
rem DualSense/DualShock/Switch support (with rumble) without DS4Windows.
set "SDL_JOYSTICK_HIDAPI=1"
set "SDL_JOYSTICK_HIDAPI_PS5=1"
set "SDL_JOYSTICK_HIDAPI_PS4=1"
set "SDL_JOYSTICK_HIDAPI_SWITCH=1"
set "SDL_JOYSTICK_HIDAPI_XBOX=1"
rem Keep reading the pad when the game window is not focused.
set "SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1"
rem Community mappings for generic controllers (also passed below).
set "SDL_GAMECONTROLLERCONFIG_FILE=%CONTROLLER_DB%"

set "CONTROLLER_ARGS=--input_backend=sdl"
if exist "%CONTROLLER_DB%" (
    set CONTROLLER_ARGS=--input_backend=sdl "--hid_mappings_file=%CONTROLLER_DB%"
) else (
    echo WARNING: controllers\gamecontrollerdb.txt not found; generic pads may not map.
)
