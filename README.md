# ludusavo

[![GitHub Stars](https://img.shields.io/github/stars/n3d06/ludusavo?style=flat-square)](https://github.com/n3d06/ludusavo/stargazers)
[![License](https://img.shields.io/github/license/n3d06/ludusavo?style=flat-square)](LICENSE)

A Windows desktop application (C# / .NET 8 WPF) that scans, backs up, and synchronizes game saves to your personal GitHub repository, powered by Ludusavi manifest data.

## Features

- **Fluent Design**: Windows 11 UI with Mica backdrop and dark mode via WPF-UI.
- **Smart Save Detection**: Locates save files for thousands of PC games using Ludusavi manifest database.
- **GitHub Cloud Storage**: Direct backup and restoration through personal GitHub repository via REST API.
- **Steam Store Integration**: Retrieves official game banner artwork on demand via Steam Store API.
- **High Refresh Rate Support**: Automatically adapts to your monitor refresh rate (60Hz to 240Hz+) with smooth scrolling.
- **Clean Storage**: All configuration, token, and backup files are saved in `Documents\ludusavo`, keeping the source and program folders clean.

## Requirements

- Windows 10 / 11
- .NET 8.0 Runtime or SDK

## Quick Start

1. Run the launcher:
   ```cmd
   Run-ludusavo.bat
   ```
2. Open the **Settings** tab, fill in your GitHub Personal Access Token (with `repo` scope), GitHub username, and repository name.
3. Click **Luu cau hinh** (Save Settings) and start managing your saves.

## Build

Build from command line:
```powershell
dotnet build ludusavo.sln -c Release
```

Publish to a folder:
```powershell
dotnet publish ludusavo/ludusavo.csproj -c Release -o publish
```
Output files will be generated in `publish/`.
