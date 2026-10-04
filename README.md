# ludusavo
 
> A modern Windows desktop app (C# / .NET 8 WPF) that automatically backs up and syncs game saves to your private GitHub repository, powered by [Ludusavi](https://github.com/mtkennerly/ludusavi) manifest data.

---

## ✨ Features

- **Clean Native Desktop UI** — Built with native WPF, crisp typography, and smooth responsiveness.
- **System Tray Integration**
  - Runs silently in the background notification area.
  - Minimize to tray on close, or launch minimized at Windows startup.
  - Automatic memory trimming to only ~15-20 MB RAM when idle in tray.
- **Smart Save Detection (50,000+ Games)**
  - Auto-detects save paths for thousands of games on Windows (`%APPDATA%`, `%LOCALAPPDATA%`, `Saved Games`, Documents, etc.).
  - Includes Steam App ID and authentic game poster banner art.
  - Auto-downloads and caches game manifest into `Documents/ludusavo/data/cache` on first launch.
- **Custom Game Support**
  - Add games not in the database with custom folder paths and Steam ID or image URLs.
- **Secure Cloud Storage via Private GitHub Repo**
  - Direct communication with GitHub REST API via Personal Access Token.
  - Token stored locally and encrypted — no third-party servers involved.
- **ZIP Packaging & Data Integrity (SHA-256)**
  - Compresses saves into timestamped ZIP archives with relative path preservation.
  - SHA-256 checksum comparison: `In sync`, `Local is newer`, `Cloud is newer`, `Local only`, `Cloud only`.

---

## 🚀 Getting Started

### Requirements
* **Windows 10 / 11 (64-bit)**
* **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)**
  > 💡 *Note: If your PC does not have .NET 8 Desktop Runtime installed yet, Windows will automatically pop up a prompt on first launch asking: **"You must install .NET Desktop Runtime to run this application. Would you like to download it now?"** Simply click **Yes** to install it once.*

### Installation
1. Go to the [**Releases**](https://github.com/3ky4r0/ludusavo/releases) page.
2. Download the latest release: `ludusavo-vX.Y.Z-win-x64.zip`.
3. Extract the ZIP to any folder and double-click `ludusavo.exe`.

### Configuration
1. Open the **Settings** tab in the app.
2. Enter your:
   - **GitHub Personal Access Token** (classic token with `repo` scope).
   - **GitHub Owner** (your GitHub username).
   - **GitHub Repository** (name of your private repository, e.g. `game-saves`).
3. Click **Test Connection**, then click **Save Settings**.
4. Go back to **Games** to scan your games and start backing up!

---

## 🛠️ Build & Publish

### Development Prerequisites
- Windows 10/11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Visual Studio 2022 / Rider / VS Code

### 1. Build Solution
```powershell
dotnet build ludusavo.sln -c Release
```

### 2. Publish Lightweight Distribution
```powershell
dotnet publish ludusavo/ludusavo.csproj -c Release -r win-x64 --self-contained false -o dist/app
```

---

## 📂 Project Structure

```
├── ludusavo.sln              # Visual Studio solution
├── assets/                   # App icons and graphics
├── data/cache/               # Local cache for manifest & game banners
└── ludusavo/                 # WPF Desktop source code (.NET 8)
    ├── Converters/           # XAML Value Converters (Thumbnails, Status, etc.)
    ├── Models/               # Data models (GameEntry, AppSettings, DetectedGame)
    ├── Services/             # Business logic (GitHub, Backup, Scanner, Manifest, Memory)
    ├── ViewModels/           # MVVM ViewModels (CommunityToolkit.Mvvm)
    └── Views/                # XAML UI (MainWindow, Pages, Dialogs)
```
