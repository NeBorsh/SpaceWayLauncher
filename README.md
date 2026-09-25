# SpaceWay Launcher

An unofficial launcher for [Space Station 14](https://spacestation14.com/).

It connects to the same servers as the official launcher and uses the same
engine builds, but focuses on a cleaner interface and features for players
who juggle several accounts, hubs and servers.

## Features

- **Server list from multiple hubs**, merged and deduplicated by address.
  Search, filters (full, empty, 18+, language) and four sort orders.
- **Favorites** with live data from hubs, custom names, and servers added
  directly by address for private or unlisted servers.
- **Hubs** can be added, disabled and reordered by drag and drop.
- **Multiple accounts** across different auth servers, including custom auth
  servers, two-factor authentication and offline play without authentication.
- **Client-side mods**: drop a `Content.*.dll` into the Mods section and enable it.
  If the engine sandbox rejects a mod, the launcher explains why and offers
  to launch without mods.
- **Replays and content bundles** can be opened from a file or dropped onto the window.
- English and Russian interface, switchable without a restart.

## Installing

Download `SpaceWayLauncher-<version>-setup.exe` from
[Releases](../../releases) and run it. The installer does not require
administrator rights and installs into `%LocalAppData%\Programs\SpaceWayLauncher`.

Uninstalling removes the program but keeps accounts, favorites and downloaded
content. Those can be cleared from the launcher settings.

Only Windows builds are published for now. The code supports Linux and macOS,
but those platforms have not been tested.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/SpaceWay.Launcher
```

### Standalone build

```
dotnet publish src/SpaceWay.Launcher -c Release -r win-x64 --self-contained true -o artifacts/win-x64
dotnet publish src/SpaceWay.Loader   -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Both projects must be published into the same directory: the launcher looks
for the game loader next to its own executable. Single-file publishing is not
supported for the same reason.

### Installer

```
powershell -ExecutionPolicy Bypass -File installer/build.ps1
```

Publishes the launcher and loader and packs them into
`artifacts/SpaceWayLauncher-<version>-setup.exe`. Requires
[Inno Setup](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`). The version is taken from
`Directory.Build.props`.

## Project layout

```
src/SpaceWay.Core          Domain logic, no UI dependencies
├── Accounts               Auth API, token storage, username validation
├── Connecting             Server queries and game launch
├── Content                Server content: incremental downloads, blob database
├── Data                   SQLite, migrations, stores
├── Engine                 Engine builds: manifest, signatures, downloads
├── Favorites              Favorite servers
├── Hubs                   Hubs, server list, filters
├── Localization           Fluent (.ftl) translations
├── Mods                   Mod library and overlay
└── Util                   Files, downloads, NTFS compression

src/SpaceWay.Launcher      Avalonia UI
src/SpaceWay.Loader        Starts the engine: assemblies from the zip, files from the database
vendor/                    Code taken from SS14.Launcher (MIT)
samples/                   Example client mods
tests/SpaceWay.Core.Tests  Unit and integration tests
```

## Data locations

| What | Where |
|---|---|
| Settings, accounts, favorites | `%AppData%\SpaceWayLauncher\settings.db` |
| Downloaded content | `%LocalAppData%\SpaceWayLauncher\content.db` |
| Engine builds | `%LocalAppData%\SpaceWayLauncher\engines\` |
| Launcher and game logs | `%LocalAppData%\SpaceWayLauncher\logs\` |

The launcher does not share data with the official launcher.

## License

[MIT](LICENSE). Parts of the code are taken from
[SS14.Launcher](https://github.com/space-wizards/SS14.Launcher), also under
the MIT License. See [NOTICE](NOTICE) for details.

This project is not affiliated with Space Wizards Federation.
