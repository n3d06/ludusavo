@echo off
title sav CLI
cd /d "%~dp0"

dotnet run --project ludusavo\ludusavo.csproj --no-launch-profile -- %*

