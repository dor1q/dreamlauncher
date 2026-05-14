# Dream Launcher

Dream Launcher is a C# + WPF desktop launcher for the Dream project. It is focused on a reliable local game launch flow: Discord authorization, Dream backend exchange-code creation, local build selection, service checks, launch control, and exportable diagnostics.

## Current Status

| Area | State |
| --- | --- |
| Desktop shell | WPF app on .NET 8 with game-style left navigation |
| Authentication | Browser Discord OAuth started and completed through the Dream backend |
| Backend identity | Dream launcher session exchange through the Dream backend |
| Backend status | Reads `/launcher/api/status` and logs service-level health |
| Local library | Build manifest plus existing folder import |
| Launch flow | Executable validation, exchange-code placeholders, process start |
| Runtime control | Launch state, known process close action, runtime log |
| Diagnostics | Exportable report without saved tokens or secrets |

The repository does not include game files, proprietary assets, private keys, Discord client secrets, or saved user sessions.

## Quick Start

```powershell
cd "D:\Dream Launcher"
dotnet run
```

If `dotnet` is not available in PowerShell yet, use the installed SDK directly:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" run --project "D:\Dream Launcher\DreamLauncher.csproj"
```

## Build

```powershell
dotnet build
```

Verified command on the current machine:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "D:\Dream Launcher\DreamLauncher.sln"
```

## Requirements

| Requirement | Why it is needed |
| --- | --- |
| Windows 10/11 | WPF desktop runtime |
| .NET 8 SDK | Build and run the launcher |
| Visual Studio with .NET desktop development | Comfortable WPF editing and debugging |
| Git | Version control and GitHub push |
| Dream backend | Exchange Discord login for a launch exchange code |
| MongoDB | Backend database for the Dream server workspace |
| Discord application | OAuth client id, secret, and redirect URI configured on the backend |

For the machine-specific checklist, see [docs/SETUP.md](docs/SETUP.md).

## Repository Layout

| Path | Purpose |
| --- | --- |
| `App.xaml`, `App.xaml.cs` | WPF application entry |
| `MainWindow.xaml`, `MainWindow.xaml.cs` | Main launcher UI and interaction flow |
| `Models/` | Settings, Discord, manifest, launch, and status models |
| `Services/` | Auth, backend exchange, manifests, launch, settings, reports |
| `config/builds.example.json` | Example local build manifest copied on first run |
| `ROADMAP.md` | Product and engineering roadmap |
| `docs/SETUP.md` | Local environment setup checklist |

## Runtime Files

The launcher stores local machine/user state under `%APPDATA%\Dream Launcher`.

| File | Contents |
| --- | --- |
| `settings.json` | Backend URL, server host/port, Discord callback port |
| `discord-session.json` | Saved local Dream launcher session |

Do not commit files from `%APPDATA%`, client secrets, access tokens, refresh tokens, or local game builds.

## Build Manifest

On first run, the app copies `config/builds.example.json` to `config/builds.json`. Edit `config/builds.json` for local paths on this machine.

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
  ],
  "env": {}
}
```

Supported launch argument placeholders:

| Placeholder | Value |
| --- | --- |
| `{exchangeCode}` | One-time code returned by the Dream backend |
| `{accountId}` | Dream account id |
| `{displayName}` | Account display name |
| `{discordId}` | Discord user id |

## Discord OAuth Setup

1. Create an application in the Discord Developer Portal.
2. Open OAuth2 settings.
3. Add this redirect URI:

```text
http://127.0.0.1:53121/callback/
```

4. Put the application Client ID and Client Secret into backend `.env`:

```env
DISCORD_CLIENT_ID=
DISCORD_CLIENT_SECRET=
```

5. Keep the launcher callback port at `53121`, or change the Discord redirect URI to match the port entered in the launcher.
6. Do not put the Client Secret into launcher settings or commit it to the repository.

## Backend Contract

For service status, the launcher calls:

```text
GET /launcher/api/status
```

Expected backend status response includes overall status, uptime, and service entries for backend API, MongoDB, XMPP, and matchmaker.

Before launch, the launcher calls:

```text
GET /launcher/api/auth/discord/start
POST /launcher/api/auth/discord/callback
```

The backend owns the Discord OAuth client secret and returns a short-lived Dream launcher session. Then the launcher calls:

```text
POST /launcher/api/auth/discord/exchange
```

Expected backend behavior:

1. Create a Discord OAuth authorization URL for the local loopback callback.
2. Exchange the returned Discord OAuth code on the backend.
3. Create or find the Dream account linked to the Discord user id.
4. Return a short-lived one-time exchange code.
5. Let the launcher inject the code into launch arguments.

## Development Flow

```powershell
dotnet build
dotnet run
```

Before committing:

1. Build the solution.
2. Confirm no secrets or local sessions are staged.
3. Keep roadmap changes aligned with actual launcher behavior.

See [ROADMAP.md](ROADMAP.md) for planned work.
