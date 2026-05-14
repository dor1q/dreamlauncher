# Local Setup Checklist

This checklist covers the tools needed to work on Dream Launcher and the connected Dream backend workspace on this machine.

## Current Machine Check

Checked on 2026-05-13.

| Tool | Current state | Action |
| --- | --- | --- |
| Git | Installed: `2.54.0.windows.1` | No action needed |
| .NET 8 SDK | Installed at `C:\Program Files\dotnet` | Added to user PATH; restart PowerShell/Codex to pick it up |
| WPF desktop runtime | Installed with .NET 8 Windows Desktop runtime | No action needed for CLI build |
| Node.js / npm | Installed: Node `v24.15.0`, npm `11.12.1` | Prefer Node.js LTS 22 for backend stability |
| MongoDB | Installed at `D:\Program Files\MongoDB\Server\8.3\bin`; service `MongoDB` is running automatically | Added MongoDB `bin` to user PATH; restart PowerShell/Codex to pick it up |
| Visual Studio | Visual Studio Community 2026 installed | `.NET desktop development` workload is still not registered; CLI build works without it |
| C++ Build Tools | Native C++ tools installed | Use Developer PowerShell so `cl.exe` and `msbuild.exe` are available |

## Required For Launcher Work

1. .NET 8 SDK.
2. Windows Desktop runtime.
3. Visual Studio with `.NET desktop development` for XAML designer and debugging.
4. Git.
5. Discord application configured for backend-owned OAuth login.
6. A local content directory for imported or future downloaded builds. Default: `Documents\Dream Builds`.

## Required For Backend Work

1. Node.js LTS.
2. npm.
3. MongoDB running locally.
4. Backend `.env` configured for MongoDB and Discord integration:

```env
DISCORD_CLIENT_ID=
DISCORD_CLIENT_SECRET=
```

The Discord redirect URI in the Developer Portal must include:

```text
http://127.0.0.1:53121/callback/
```

## PATH Fixes

These directories have been added to the user PATH on this machine. Restart PowerShell or Codex before relying on short commands like `dotnet` and `mongod`:

```text
C:\Program Files\dotnet
D:\Program Files\MongoDB\Server\8.3\bin
```

Until PATH is fixed, use full paths:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "D:\Dream Launcher\DreamLauncher.sln"
& "D:\Program Files\MongoDB\Server\8.3\bin\mongod.exe" --version
```

## Visual Studio Workloads

Open Visual Studio Installer and make sure these workloads are installed:

| Workload | Needed for |
| --- | --- |
| `.NET desktop development` | WPF launcher editing, debugging, designer support |
| `Desktop development with C++` | Native game-server or C++ project work |

## Smoke Checks

```powershell
git --version
dotnet --info
node -v
npm -v
mongod --version
```

If `dotnet` or `mongod` fails but the full-path command works, the tool is installed and only PATH needs fixing.
