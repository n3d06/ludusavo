@echo off
title sav CLI
cd /d "%~dp0"

dotnet run --project src\sav.csproj --no-launch-profile -- %*

