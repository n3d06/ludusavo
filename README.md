[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![Storage](https://img.shields.io/badge/Storage-Google_Drive-4285F4?logo=googledrive)](https://drive.google.com/)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)

A command-line tool to backup, restore, and sync game saves to your personal Google Drive, powered by the Ludusavi database.

---

## Features

- Automatic game save detection on Windows (`%APPDATA%`, `%LOCALAPPDATA%`, `Saved Games`, `Documents`, etc.).
- Direct synchronization with Google Drive via OAuth2.
- ZIP compression with path mapping for easy restoration across different machines.
- Flexible selection: single game, multiple games separated by spaces (`1 3 5`), or all (`all`).
- Fast and lightweight CLI interface.

---

## Commands

```bash
sav [command] [arguments]
```

| Command | Description | Example |
| :--- | :--- | :--- |
| `sav ls` | List detected games on local machine | `sav ls` |
| `sav lsc` | List game saves available on Google Drive | `sav lsc` |
| `sav scan` | Rescan local machine for game saves | `sav scan` |
| `sav push` | Backup and upload saves to Google Drive | `sav push`<br>`sav push 1`<br>`sav push 1 3 5`<br>`sav push all` |
| `sav pull` | Download saves from Google Drive and restore to local machine | `sav pull`<br>`sav pull 1`<br>`sav pull 1 3 5`<br>`sav pull all` |
| `sav status` | Check connection status and Google Drive quota | `sav status` |
| `sav login` | Log in to Google Drive account via browser | `sav login` |
| `sav logout` | Log out from Google Drive account | `sav logout` |
| `sav config` | View or set Google Client ID and Secret | `sav config` |
| `sav add` | Add a custom game save location | `sav add` |
| `sav remove` | Remove custom game by index | `sav remove 1` |
| `sav help` | Display help information | `sav help` |

> Running `sav` without arguments displays the help screen.

---

## Installation & Usage

### Installer (Recommended)
1. Download `sav-setup.exe` from the latest GitHub Release.
2. Run the installer to automatically configure `PATH`.
3. Open Terminal / PowerShell and type `sav`.

### Build from source
```powershell
dotnet build -c Release
.\sav.bat status
.\sav.bat push 1 3 5
```
 