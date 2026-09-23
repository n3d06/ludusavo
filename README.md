# ludusavo

> A modern Windows desktop app (C# / .NET 8 WPF) that automatically backs up and syncs game saves to your private GitHub repository, powered by [Ludusavi](https://github.com/mtkennerly/ludusavi) manifest data.

---

## ✨ Features

- **Windows 11 Fluent UI** — Built with WPF-UI, featuring Mica Backdrop, Dark Mode, and smooth animations.
- **System Tray Integration**
  - Runs silently in the notification area.
  - Minimize to tray on close or launch hidden with `--minimized`.
  - Quick-access context menu: Open, Sync All, Exit.
- **Smart Save Detection (Ludusavi Manifest)**
  - Auto-detects save paths for thousands of games on Windows (`%APPDATA%`, `%LOCALAPPDATA%`, `Saved Games`, Documents, etc.).
  - Includes Steam App ID and game poster art.
- **Secure Cloud Storage via Private GitHub Repo**
  - Communicates directly with the GitHub REST API from C#.
  - Token stored locally (`.env` or `appsettings.json`) — no third-party servers involved.
- **ZIP Packaging & Data Integrity (SHA-256)**
  - Compresses saves into ZIP archives with relative path metadata.
  - SHA-256 checksum comparison to determine sync status: `Synced`, `LocalNewer`, `RemoteNewer`, `LocalOnly`, `RemoteOnly`.
- **Auto-Sync on Game Exit**
  - Detects when a game process ends and automatically uploads the latest saves.

---

## 🚀 Getting Started

### 1. Quick Launch
Double-click the launcher:
```
Run-ludusavo.bat
```
It will automatically find an existing build (Publish / Release / Debug) or fall back to `dotnet run`.

### 2. Configure GitHub Token
Open the **Settings** tab in the app, or create a `.env` file in the project root:

```env
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_OWNER=your_username
GITHUB_REPO=your_save_repo
```

---

## 🛠️ Build & Publish

### Prerequisites
- Windows 10/11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Visual Studio 2022 / Rider / VS Code (optional)

### 1. Open in Visual Studio
Open `ludusavo.sln` or `ludusavo.slnx` from the project root.

### 2. Build from Command Line
```powershell
dotnet build ludusavo.sln -c Release
```

### 3. Publish as Single-File Executable
```powershell
dotnet publish ludusavo/ludusavo.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```
The standalone executable will be created at `publish/ludusavo.exe`.

---

## 📂 Project Structure

```
├── ludusavo.sln              # Visual Studio solution
├── ludusavo.slnx             # Modern solution format
├── Run-ludusavo.bat          # Quick launcher
├── .env                      # GitHub credentials (local only)
├── assets/                   # App icons and images
├── data/
│   ├── cache/                # Ludusavi manifest & game poster cache
│   └── manifest/             # Raw Ludusavi data
├── publish/                  # Published executables
└── ludusavo/                 # WPF Desktop source code (.NET 8)
    ├── Models/               # Data models (GameEntry, AppSettings, etc.)
    ├── Services/             # Business logic (GitHub, Backup, Restore, Scanner, Watcher, Manifest)
    ├── ViewModels/           # MVVM ViewModels (CommunityToolkit.Mvvm)
    ├── Views/                # XAML UI (MainWindow, Pages, Dialogs)
    └── Converters/           # XAML Value Converters
```
