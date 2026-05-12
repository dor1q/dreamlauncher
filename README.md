# Dream Launcher

C# + WPF desktop launcher MVP for Dream.

## Current MVP

- Discord OAuth login.
- Backend URL settings.
- Game server TCP status check.
- Local build manifest in `config/builds.json`.
- Build picker.
- Existing build folder import.
- Close known Fortnite/Epic processes from the launcher.
- Selected executable launch after Discord login.
- Dream backend exchange-code request before launch.
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
  "arguments": [
    "-AUTH_LOGIN=unused",
    "-AUTH_PASSWORD={exchangeCode}",
    "-AUTH_TYPE=exchangecode"
  ]
}
```

Settings are stored in `%APPDATA%\Dream Launcher\settings.json`.
Discord sessions are stored in `%APPDATA%\Dream Launcher\discord-session.json`.

## Discord OAuth setup

1. Create an application in the Discord Developer Portal.
2. In OAuth2 settings, add this redirect URI:

```text
http://127.0.0.1:53121/callback/
```

3. Put the application Client ID into the launcher settings.
4. Put the Client Secret into the launcher settings. It is saved only in the local `%APPDATA%` settings file.
5. Keep the launcher callback port at `53121`, or change the Discord redirect URI to match the port you enter.

Do not commit a Discord client secret, bot token, or saved session file.

## Backend integration

The launcher calls:

```text
POST /launcher/api/auth/discord/exchange
```

The backend validates the Discord access token, finds the Dream account by `discordId`, and returns a one-time exchange code. Build arguments can use these placeholders:

- `{exchangeCode}`
- `{accountId}`
- `{displayName}`
- `{discordId}`
