# KlangHub — Project Checkpoint (2026-07-04)

**HEAD:** `a632cf6` · **Branch:** `master` · **Tests:** 65 green (`dotnet test`) · **Working tree:** clean
**Latest verified build/zip:** `dist/KlangHub-Release-a632cf6.zip` (framework-dependent, needs .NET 8 Desktop Runtime)

This document is the authoritative resume point. Paste the "Quick-start for a new chat" block (bottom) into a
new session.

---

## 1. What KlangHub is
A Windows / .NET 8 WinForms app that captures the PC's audio and casts it to Chromecast devices (mid-rebrand
from "ChromeCast-Desktop-Audio-Streamer"). Chromecast is a **pull** model: the app runs an HTTP streaming
server; the device connects and pulls the stream.

## 2. Architecture (compiler-enforced 3-project split)
- **KlangHub.Core** (`net8.0`, neutral) — `Core.Casting` (ICastProvider, IPlaybackSession, IDeviceDiscovery,
  CastDeviceDescriptor, ProviderId, CastProviderCapabilities + DeliveryModel/RequiresPairing, ICastHost,
  CompositeCastProvider, PlaybackState, VolumeStatus), `Core.Audio` (AudioFormat, AudioFrame, IAudioEncoder,
  **IAudioSink**), Core.Diagnostics.ILogger, localized Strings.
- **KlangHub.Platform** (`net8.0-windows`, `UseWindowsForms=false`) — Chromecast impl (Device, Communication,
  Discover/DiscoverDevices, Streaming, ProtocolBuffer), `Platform.Audio` (WaveFormatMapping, Mp3Encoder),
  `Platform.Casting.{Chromecast,AirPlay,Snapcast,Shared}`, Windows utilities. Has
  `InternalsVisibleTo("KlangHub.Tests")` (in Platform.csproj).
- **KlangHub** (App, `net8.0-windows` WinExe) — WinForms shell + orchestration. `Application.Orchestration`
  (Orchestrator, SettingsService, **ChromecastAudioSink**) is WinForms-free + testable. Note: App sets
  `GenerateAssemblyInfo=false`, so `InternalsVisibleTo` must be declared in code — see
  `Source/KlangHub/AssemblyAttributes.cs`.
- **KlangHub.Tests** (`net8.0-windows`) — xUnit + NSubstitute. **65 tests.**

Reference direction: App → Platform → Core.

## 3. Completed phases (all on master)
- **H1–H4c** — the 3-project split + WaveFormat→AudioFormat neutralization + Platform extraction + App
  slim-down + `CompositeCastProvider` multi-provider seam + ApplicationLogic decomposed into WinForms-free
  **Orchestrator** + **SettingsService** (thin ApplicationLogic facade + tray/settings shell) + first tests.
- **M1** (`ddf8629`) — multi-protocol design doc: `docs/M1-multi-protocol-design.md`.
- **M2** — abstraction extensions: **M2-1** `CastProviderCapabilities` + DeliveryModel/RequiresPairing;
  **M2-2** `IPlaybackSession` control methods → **Task**-returning (Chromecast returns Task.CompletedTask);
  **M2-3** neutral **`IAudioSink`** seam — the Orchestrator fans raw PCM to sinks; Chromecast encode/format/lag
  moved into `ChromecastAudioSink` (byte-identical, unit-tested).
- **M3** — structure + discovery: **`Shared/MdnsDiscovery`** (generic Tmds.MDns browser, any service type),
  **`SnapcastProvider`/`SnapcastDiscovery`** (`_snapcast._tcp`, discovery-only, CreateSession throws),
  **`AirPlayProvider`/`AirPlayDiscovery`** (`_airplay._tcp`+`_raop._tcp`, TXT classification legacy-RAOP vs
  AirPlay2, discovery-only). Both registered in the composite; non-Chromecast discoveries logged.

## 4. Chromecast discovery robustness — the "Enchant Speaker" saga (RESOLVED ✅, HW-confirmed by the maintainer)
A real user network (Harman Kardon **Enchant Speaker** [Chromecast-built-in], Samsung **Soundbar** HW-Q995GD,
**Google TV**, **TCL TV**, **the multi-room group** group) exposed a chain of discovery bugs. Fixed across
`2eae1cb → 163c09f → 03cf6e0 → 9ae31bb → 55ac11f → fcad43a → 61c814d → a632cf6`. Final working state:
1. **eureka_info fallback** (`2eae1cb`) — devices that don't serve `http://<ip>:8008/setup/eureka_info` are
   still added from mDNS (matches other cast apps).
2. **Prefer/require IPv4** (`163c09f`→`03cf6e0`) — Tmds.MDns can list IPv6 first; a raw IPv6 breaks the eureka
   URL and the `:8009` TLS connect. `DiscoverDevices.OnServiceAdded` now **skips IPv6-only announcements**
   (devices arrive via their IPv4 announcement/eureka).
3. **THE key insight — the placeholder MAC `00:00:00:00:00:00`** (reported by Google TV / Android TV / the
   Enchant in eureka) had to be treated as "no identity" in **TWO** places:
   - `Devices.GetDevice` (`fcad43a`, `HasRealMac`) — list dedup by MAC only for real MACs, else by IP →
     each device is its own entry (not collapsed).
   - `ChromecastDeviceId.From` (`a632cf6`) — the descriptor/session id skips the placeholder MAC and falls
     through to `IP:port` → each tile resolves to its own session (no "3× Enchant tiles ganged together").
   Both were needed: list dedup AND descriptor/session id.

**Diagnostic infra kept:** `DiscoverDevices` takes an `ILogger` and logs each raw mDNS announcement
(type/fn/all-addresses/chosen-or-skipped); `SetDeviceInformation` logs eureka name/ip/mac. Visible in the app
via Options → "Log device communication" (adds the hidden **Log** tab; logs go to the `txtLog` textbox, not
disk).

**Session lesson:** on this fragile discovery path, *instrument first, then fix* — several rounds were lost to
guessing before the full log made the placeholder-MAC root cause obvious. Don't bundle speculative hardening
with a targeted fix here.

## 5. Roadmap (next)
- **M4 — Snapcast control-plane** (recommended next feature): `SnapcastProvider.CreateSession` → a real
  JSON-RPC 2.0 / WebSocket session to an **existing** snapserver (connect-to-existing per decision D2):
  `Server.GetStatus` + `Client.SetVolume` + `Group.SetStream` + `On*` events. Structure/discovery already
  exist. Snapserver doesn't run on Windows, so KlangHub connects to one on the LAN (Linux/Pi/Docker).
- **AirPlay streaming** — a large, dedicated later spike (transient HomeKit pairing SRP-6a + X25519/Ed25519 +
  ChaCha20 + ALAC/RTP; no usable managed .NET sender → port/interop the Apache-2.0 `airplay2-sender-cpp`).
  Discovery already works. Note: the Enchant/TCL/Soundbar are AirPlay2-capable too.
- **Housekeeping (non-blocking):** net8→net10 LTS bump (low-effort; net8 EOL 2026-11-10); move
  Orchestrator/SettingsService to a `KlangHub.App.Core` lib so Tests needn't reference the WinExe; namespace
  tidy (IDevice/Device still `KlangHub.Application`).

## 6. Open decisions already made (the maintainer)
- D1 void→Task: **yes** (done in M2-2). · D2 Snapcast: **connect to existing server** first. ·
  D3 AirPlay: **discovery-first**, pairing/streaming a later spike. · D4 licensing: node_airtunes2=AGPL
  (reference only), airplay2-sender-cpp=Apache (port ok).

## 7. Working protocol with the maintainer
German-speaking lead architect. Every sprint: he posts a detailed spec → present a comprehensive plan with
**honest risk analysis ("kein reines Lob")** → he gives explicit "Go" → implement + build + smoke-test +
commit (strictly behaviour-preserving) → report honestly. Large ambitious-but-realistic sprints. Always
surface honest risks/caveats.

## 8. Build / dev environment notes
- SDK: `%USERPROFILE%\.dotnet\dotnet.exe` (8.0.422, per-user, not on PATH).
- Build the **.sln** in Release (`dotnet build Source/KlangHub.sln -c Release`) — the legacy `Setup Project`
  vdproj is skipped by the CLI. Output: `Source/KlangHub/bin/Release/net8.0-windows/KlangHub.exe`.
- **Kill running `KlangHub.exe` before a Release build** — a running instance locks Core/Platform DLLs
  (MSB3021/3027).
- `dist/` is gitignored; zips are `dist/KlangHub-Release-<shorthash>.zip` (framework-dependent, ~83 MB).
- Commit messages via `git commit -F <file>` (PowerShell here-strings mangle multi-line `-m`; `<<` heredocs
  don't exist in PowerShell). The Bash tool's `Remove-Item` with a wildcard can be sandbox-blocked — delete
  zips by exact `-LiteralPath`.
- git core.autocrlf=true → "LF will be replaced by CRLF" warnings are harmless.

---

## Quick-start for a new chat (paste this)

> KlangHub (Windows/.NET8 WinForms Chromecast audio caster). We're at commit `a632cf6` on master, 65 tests
> green, working tree clean. The full state is in `docs/CHECKPOINT-2026-07-04.md` (read it). Summary:
> 3-project split (Core/Platform/App) done; Orchestrator + SettingsService + CompositeCastProvider +
> IAudioSink seam + Task-based control all in place (phases H1–H4c, M1, M2, M3). The Chromecast
> discovery-robustness saga (the Harman Kardon "Enchant Speaker" multi-device network) is **resolved and
> hardware-confirmed** — the key was treating the placeholder MAC `00:00:00:00:00:00` as "no identity" in
> BOTH `Devices.GetDevice` (list dedup) AND `ChromecastDeviceId.From` (session id). Next logical feature is
> **M4: Snapcast control-plane** (connect to an existing snapserver via JSON-RPC/WebSocket); AirPlay
> streaming is a later dedicated spike. Please read the checkpoint doc, then propose a plan for the next
> step with honest risk analysis before implementing.
