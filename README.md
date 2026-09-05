# KlangHub

**One music. Every room.**

*[Deutsche Fassung](README.de.md)*

A native Windows hub for desktop audio. It casts to every room in the house today, and its
architecture is built so that further protocols arrive as providers rather than as rewrites.

`Version 0.0.1` · Windows 10/11 · .NET 10 · Chromecast

This is a foundation release, not a 1.0 feature dump. What it does, it does on real hardware; what it
does not do is named below rather than implied.

<!-- Hero image goes here once a screenshot exists that is safe to publish.
     Required file, size and the capture checklist: docs/assets/README.md
     Nothing in this repository may show an address, a device list or a window title. -->

## Why it exists

- Windows has no serious native multiroom hub. Casting the desktop is either a browser tab or a
  utility that forgets what is playing.
- Casting from a desktop should keep metadata, zones and quality — not reduce a lossless library to
  an anonymous stream with no title on the screen.
- Protocols must be replaceable without rewriting the application.

## What 0.0.1 actually does

- **Capture** desktop output or a microphone / line-in input (WASAPI loopback).
- **Discover** Chromecast and Google Cast devices and groups over mDNS.
- **Cast to several rooms at once**, including Google Cast groups.
- **Choose the stream format**: WAV 16/24/32-bit, FLAC (lossless), MP3 128/320. A fresh installation
  streams FLAC with a ten-second buffer.
- **Control each room** — play, pause, stop, volume, mute — through `IPlaybackSession`, with a
  per-room volume ceiling and a room fader that keeps the balance between its speakers.
- **Name rooms and group the cards by room**, with room presets.
- **Show what is playing.** A five-source metadata cascade (Windows now-playing session, Clementine's
  network remote, file tags, player window title, file name) resolves to one answer and travels with
  the stream.
- **Run from the notification area**, start with Windows, respond to optional shortcuts.
- **24 languages** — every official EU language, each fully translated, listed under its own name.

The multi-provider seam is already in place: AirPlay and Snapcast endpoints are **discovered and
classified**, and are deliberately not playable — see the roadmap.

## Architecture

```
KlangHub (WinForms shell, Orchestrator)
   │
   ├─► ICastProvider ── CompositeCastProvider
   │                       ├─ ChromecastProvider   full session
   │                       ├─ SnapcastProvider     discovery only
   │                       └─ AirPlayProvider      discovery only
   │
   ├─► IAudioCaptureEngine → IAudioSink[] → IAudioEncoder → local HTTP
   │
   └─◄ IPlaybackSession    play · pause · stop · volume · mute · status

KlangHub.Core       contracts and rules — no Windows, no UI, no protocol
KlangHub.Platform   Chromecast, audio capture and encoding, Windows services
```

Core references nothing, Platform references Core, the app references both, and there is no path
back. A second protocol costs a class and one line in the composition root.

The full picture, including what 0.0.1 deliberately does *not* abstract:
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## TV stage

`receiver/` holds a custom CAF v3 web receiver: a cinematic now-playing screen with the track, the
cover and the format, in place of Google's default receiver. It is a separate web page served over
HTTPS, not part of the application binary — audio stays on the native CAF player and never passes
through it.

It is complete in this repository and **has never run on real hardware**: it needs a hosted URL and a
registered Cast application id. Setup and current status are in
[`receiver/README.md`](receiver/README.md).

## Requirements

- Windows 10 or 11, 64-bit
- A Chromecast / Google Cast device on the same network
- Windows Firewall access for your home network, granted at first start — without it playback
  cannot begin

The shipped build is self-contained; the .NET runtime comes with the installer. Only building from
source needs the SDK.

> Desktop picture and cast audio are never in sync — buffering makes a delay unavoidable. KlangHub is
> a music hub, not a way to put a film's soundtrack on the speakers.

## Install

`KlangHub-0.0.1-Setup.exe` (Inno Setup) installs per user and needs no administrator rights.

**Windows will warn, and that is expected.** The setup is not code-signed; a certificate costs money
this project does not spend. SmartScreen shows *"Windows protected your PC"* — the way through is
**More info → Run anyway**. Anyone who would rather not do that can build from source.

## Build and test

Requires the **.NET 10 SDK**. All projects target `net10.0` or `net10.0-windows10.0.19041.0`; the
Windows SDK target is needed for the Windows now-playing API.

```
dotnet build Source/KlangHub.sln -c Release
dotnet test Source/KlangHub.Tests/KlangHub.Tests.csproj
```

The installer is a self-contained publish plus Inno Setup:

```
dotnet publish Source/KlangHub/KlangHub.csproj -c Release -r win-x64 --self-contained true -o publish/KlangHub-0.0.1-win-x64
ISCC.exe installer/KlangHub.iss
```

First cast, network requirements and the errors that show up first:
[`docs/GETTING-STARTED.md`](docs/GETTING-STARTED.md).

## Project layout

| Path | What is in it |
|---|---|
| `Source/KlangHub.Core` | Contracts and rules. No Windows, no UI, no protocol. |
| `Source/KlangHub.Platform` | Chromecast, audio capture and encoding, Windows services. |
| `Source/KlangHub` | The WinForms shell, orchestration and the composition root. |
| `Source/KlangHub.Tests` | 734 tests across all three, including a privacy check over everything git tracks. |
| `receiver/` | The custom CAF v3 web receiver for the television. |
| `installer/` | The Inno Setup script and its wizard images. |
| `docs/` | Architecture, getting started, casting, and the hard-won notes. |
| `tools/` | PowerShell helpers, including the pre-commit privacy check. |

## Roadmap

- **Now** — harden Chromecast, and finish carrying the neutral device descriptor into the user
  interface.
- **Next** — AirPlay 2. Discovery and classification exist; a session needs HomeKit pairing and an
  ALAC/RTP sender.
- **Then** — Snapcast. Discovery exists; a session needs a snapserver feed and JSON-RPC control.
- **Not planned** — Spotify Connect and Tidal Connect.

## Status

Early. Interfaces can still move, and 0.0.1 is the first cut of a shape rather than a settled one.
Issues about architecture and bugs are welcome; requests for protocols that are not on the roadmap
are not.

Nothing here claims to be finished when it is not. The receiver has not met a television, the
Orchestrator has not left the app project, and there is exactly one audio sink behind an interface
built for several.

## Contributing

How this project works — test first, new text in all 24 languages, and the rule that matters most in
a public repository — is in [CONTRIBUTING.md](CONTRIBUTING.md).

Security issues: please do not open a public issue. [SECURITY.md](SECURITY.md) explains the way.

## License

MIT — see [LICENSE](LICENSE) for the full text and attribution. The codebase began as a fork of a
Chromecast audio streamer by **SamDel** (MIT). Bundled third-party components and their licenses are
listed in [`docs/THIRD-PARTY-LICENSES.md`](docs/THIRD-PARTY-LICENSES.md).

## Trademarks and independence

KlangHub is an independent open-source project with **no connection to Google, Apple, Spotify,
Samsung or Harman**, and is neither endorsed nor certified by any of them. Product and protocol names
are used descriptively, to say which devices the application works with.

- **Chromecast**, **Google Cast** and **Google Home** are trademarks of Google LLC.
- **AirPlay** is a trademark of Apple Inc. KlangHub **discovers** AirPlay devices from their open
  mDNS announcements and sends **no** AirPlay audio stream. Doing so would require Apple's pairing
  scheme and MFi licensing — neither is implemented here, and neither is circumvented.
- **Spotify** and **Spotify Connect** are trademarks of Spotify AB. KlangHub does **not** support
  Spotify Connect.
- **Snapcast** is a project by Johannes Pohl (GPL-3.0). KlangHub discovers Snapcast endpoints;
  control is not implemented. No Snapcast code is used or linked.
