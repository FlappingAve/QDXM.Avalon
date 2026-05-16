# QDXM Avalon

![QBDXM Avalon Main Window](./-assets/main.png?raw=true)

QDXM Avalon is a redesigned Avalonia-based version of [QobuzDownloaderX-Mod](https://github.com/DJDoubleD/QobuzDownloaderX-MOD). You can only download 30 second previews with a free account. A paid account is needed to download full content.

Notable changes include:

- `QDXMA://` protocol intake for supported URLs.
- Expanded search for albums, tracks, artists, labels, and playlists.
- Folder and file templates with presets, tag options, and live destination previews.

> [!NOTE]
> AI tools are used for the majority of development for code generation, review, and refactoring.


## Directory
The app is split into:

```text
QDXM.Avalon/            Avalonia desktop app, view models, UI services, and app startup
QDXM.Avalon.Core/       Downloader, settings, tagging, protocol, search models
QDXM.Avalon.Tests/      Unit tests for parser, settings, search, protocol, and request behavior
QobuzApiSharp/          Forked Qobuz API wrapper submodule
```

## Requirements

- Windows.
- .NET SDK 8 or newer for building.
- .NET 8 Runtime / Desktop Runtime for running the app.
- The `QobuzApiSharp` submodule.

Clone with submodules:

```powershell
git clone --recurse-submodules https://github.com/FlappingAve/QBDXM.Avalon.git
```

If you already cloned without submodules, run this from the repo root:

```powershell
git submodule update --init --recursive
```

## Build and Run

From the repo root:

```powershell
dotnet build "QDXM.Avalon\QDXM.Avalon.csproj"
dotnet run --project "QDXM.Avalon\QDXM.Avalon.csproj"
```

For the usual release loop, run:

```powershell
.\build-release.bat
```

Publish the release app:

```powershell
dotnet publish "QDXM.Avalon\QDXM.Avalon.csproj" -c Release -r win-x64 --self-contained false
```

Published exe:

```text
QDXM.Avalon\bin\Release\net8.0\win-x64\publish\QDXM.Avalon.exe
```

Run tests:

```powershell
dotnet test "QDXM.Avalon.Tests\QDXM.Avalon.Tests.csproj"
```

Version numbers are centralized in `Directory.Build.props`.

## Data

Local app data sits beside the executable:

```text
<exe folder>\Avalon-Data\settings.json
<exe folder>\Avalon-Data\queue-state.json
<exe folder>\Avalon-Data\protocol-queue\
<exe folder>\Avalon-Data\logs\app.log
<exe folder>\Avalon-Data\covers\
<exe folder>\Avalon-Data\search-images\
```

User ID and auth token are stored in Windows Credential Manager. Settings, queue state, logs, cached images, and protocol handoff files stay in the local `Avalon-Data` folder beside the executable.

Signing out removes the saved user ID/auth token credential.

## Tags and Templates

Templates use brace fields such as `{AlbumTitle}`, `{TrackNumberPadded}`, `{Quality}`, and `{Work}`. Everything outside braces is treated as literal text. Use `\` or `/` in the folder template to create subfolders.

The Tags view includes contextual field buttons for each template type, a combined live destination preview, written metadata checkboxes, and work handling options for albums that expose work headings.

## Protocol

QDXM Avalon can register the `QDXMA://` protocol to the currently running executable path from Settings.

If an instance is already running, protocol URLs are written to the local protocol queue and picked up by the running app.

To import a text file of Qobuz links, run:

```powershell
QDXM.Avalon.exe --import "C:\Path\qobuz-links.txt"
```

The import file is read line by line. Blank lines are ignored.
Each non-empty line should contain one supported Qobuz URL.

## Disclaimer & Legal
I will not be responsible for how you use QDXM Avalon.
QDXM Avalon is based on prior QDX mod work by DJDoubleD.

This program ***DOES NOT*** include...

- Code to bypass Qobuz's region restrictions.
- Qobuz app IDs or secrets.

QDXM Avalon does not publish any of Qobuz's private secrets or app IDs. It contains regular expressions and other code to dynamically grab them from Qobuz's web player's *publicly available* JavaScript, which is not rehosted, but grabbed client side. It may also use public storefront JavaScript for public client-side search configuration, including Algolia search configuration. Scraping public data is not a violation of the Computer Fraud and Abuse Act (USA) according to the Ninth Court of Appeals, [case # 17-16783](http://cdn.ca9.uscourts.gov/datastore/opinions/2019/09/09/17-16783.pdf) (see page 29).

QDXM Avalon uses the Qobuz API, but is not endorsed, certified or otherwise approved in any way by Qobuz.

Qobuz brand and name is the registered trademark of its respective owner.

QDXM Avalon has no partnership, sponsorship or endorsement with Qobuz.

By using QDXM Avalon, you agree to the following: http://static.qobuz.com/apps/api/QobuzAPI-TermsofUse.pdf
