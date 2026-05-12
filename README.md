# Dream Launcher

C# + WPF desktop launcher MVP for Dream.

## Current MVP

- Backend URL settings.
- Game server TCP status check.
- Local build manifest in `config/builds.json`.
- Build picker.
- Selected executable launch.
- Runtime logs.

The project does not include game files, proprietary assets, private keys, or credentials.

## Requirements

- .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.

## Run locally

```powershell
cd "D:\Dream Launcher"
dotnet run
```

## Build

```powershell
dotnet build
```

## Build manifest

On first run, the app copies `config/builds.example.json` to `config/builds.json`.
Edit `config/builds.json` for local paths on this machine.

Example build entry:

```json
{
  "id": "season-5-local",
  "name": "Season 5 Local",
  "path": "D:\\Games\\Dream\\Season5",
  "executable": "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe",
  "arguments": []
}
```

Settings are stored in `%APPDATA%\Dream Launcher\settings.json`.
