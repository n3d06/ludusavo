@echo off
title ludusavo Launcher
cd /d "%~dp0"

if exist "%~dp0ludusavo\bin\Release\net8.0-windows\ludusavo.exe" (
    start "" "%~dp0ludusavo\bin\Release\net8.0-windows\ludusavo.exe"
    exit
)

if exist "%~dp0ludusavo\bin\Debug\net8.0-windows\ludusavo.exe" (
    start "" "%~dp0ludusavo\bin\Debug\net8.0-windows\ludusavo.exe"
    exit
)

echo [ludusavo] Dang khoi dong bang dotnet run...
dotnet run --project ludusavo\ludusavo.csproj -c Release
