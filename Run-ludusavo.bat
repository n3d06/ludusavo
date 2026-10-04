@echo off
title ludusavo Launcher
cd /d "%~dp0"

echo [ludusavo] Dang khoi dong bang dotnet run...
dotnet run --project ludusavo\ludusavo.csproj -c Release
