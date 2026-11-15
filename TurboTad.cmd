@echo off
setlocal
if not exist "%~dp0Subnautica2-Win64-Shipping.exe" (
    echo Extract TurboTad into Subnautica2\Binaries\Win64 first.
    pause
    exit /b 1
)
if not exist "%~dp0ue4ss\Mods\TurboTad\TurboTad.Controls.exe" (
    echo TurboTad companion application is missing. Extract the complete ZIP.
    pause
    exit /b 1
)
start "" "%~dp0ue4ss\Mods\TurboTad\TurboTad.Controls.exe"
