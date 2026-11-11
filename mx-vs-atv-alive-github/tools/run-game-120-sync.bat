@echo off
rem 120 fps preset with the readback copy worker disabled, for A/B testing the
rem regular (synchronous) readback path. Boolean cvars are CLI flags, so the
rem negated form is --no-<name> rather than --<name>=false.
set "MX_ALIVE_EXTRA=--no-d3d12_readback_resolve_async"
call "%~dp0run-game.bat" 120
