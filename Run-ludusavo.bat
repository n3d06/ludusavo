@echo off
title ludusavo CLI
cd /d "%~dp0"

dotnet run --project ludusavo\ludusavo.csproj --no-launch-profile -- %*
