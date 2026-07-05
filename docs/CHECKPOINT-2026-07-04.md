# KlangHub — Project Checkpoint (2026-07-04, updated 2026-07-05)

**HEAD:** `f4f9e6d` · **Branch:** `master` · **Tests:** 140 green (`dotnet test`) · **Working tree:** clean
**LATEST:** HW-test #2 pinned the "noise"/interrupts to **32-bit uncompressed LPCM OOMing small speakers
(ERROR 102)** → **FLAC is now the out-of-box default** (24-bit HiFi, lossless, COMPRESSED → no OOM; the maintainer's
call). Plus the zombie-tile id-reconcile completed (id-preservation across re-fetch). §10/§11. Ready build
`dist/KlangHub-Release-f4f9e6d.zip`. Earlier: Premium Chromecast 2026 sprint (G1–G6) + cross-service IPv4
bridge, see §9.

_(historical header below, pre-sprint HEAD was `2402f1d` / 79 tests)_
**HEAD:** `2402f1d` · **Branch:** `master` · **Tests:** 79 green (`dotnet test`) · **Working tree:** clean
**Enchant discovery is an ongoing saga** (IPv6-only-flapping); latest fix `2402f1d` = handle mDNS `ServiceChanged`
(late-A capture) + instrument the AirPlay mDNS address log — **HW-test pending**, see §4 chapters.
**Runtime:** **.NET 10** (LTS). Framework-dependent build → needs the **.NET 10 Desktop Runtime** discoverable
by the app host (installed machine-wide at `C:\Program Files\dotnet`, 10.0.9 — HW-confirmed by the maintainer).

This document is the authoritative resume point. Paste the "Quick-start for a new chat" block (bottom) into a
new session.

---

## 0. Update 2026-07-05 — .NET 10 LTS migration + package cleanup (commit `098a9d5`)
All four projects retargeted **net8 → net10** (Core `net10.0`; Platform/App/Tests `net10.0-windows`).
Package bumps: NAudio 2.2.1→**2.3.0** (stable; 3.0.0 is preview-only), Microsoft.Windows.Compatibility
9.0.3→**10.0.9**, test tooling (Microsoft.NET.Test.Sdk 18.7.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.5,
NSubstitute 5.3.0). **NAudio.Lame 2.1.0 + Tmds.MDns 0.8.0 were already latest** → untouched (also keeps the
fragile mDNS discovery path unchanged). **Removed 7 now-redundant packages** (all in-box on net10):
Microsoft.CSharp, Microsoft.VisualBasic (WindowsFormsApplicationBase ships via Microsoft.VisualBasic.Forms),
System.Memory, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe,
System.Threading.Tasks.Extensions, System.Text.Json (×2, per SDK NU1510). Build 0 errors, 65/65 green,
app smoke-tested on the net10 runtime.

## 0.1 Update 2026-07-05 — Tier 2 optimizations (commits `05f60a6`..`486f4f0`, spec `docs/superpowers/specs/2026-07-05-tier2-optimization-design.md`)
All behaviour-preserving; build 0 errors, **0 source warnings**, 65 tests green, app smoke-tested on net10.
- **A/E audio allocations** (`955270d`): removed 2 redundant per-frame MP3 copies (`ChromecastAudioSink` +
  `Mp3Stream.Encode`), swapped the 1000 Hz capture-thread LINQ `Take().ToArray()` for `AsSpan().ToArray()`,
  and made the streaming send **zero-allocation** (`StreamingConnection` now `Socket.Send(buffer,0,count)` instead
  of `Take().ToArray()→List→ToArray()`). Byte-identical (guarded by `ChromecastAudioSinkTests`). NOTE: real
  ArrayPool pooling was **rejected as unsafe** — `ApplicationBuffer.KeepABuffer` retains the frame reference, so
  frame buffers must stay fresh-per-frame.
- **B Directory.Build.props** (`660e85b`): `Source/Directory.Build.props` hoists `ImplicitUsings=disable`,
  `LangVersion=latest`, `Deterministic=true`, **`Nullable=enable`**; per-project dupes removed.
- **C Nullable enabled project-wide** (`ceb25f9`/`99ddb67`/`486f4f0`): **966 → 0** nullable warnings via
  behaviour-preserving annotations only (`?`, `= null!` for set-after-ctor/DTO fields, `!` at guarded
  use-sites, `object? sender`, `event …?`). Generated protobuf `ChromeCastAudioStream.cs` got `#nullable
  disable`. Verified pure-annotation: **0 added `??`/`?.`** in the whole nullable diff. `<Nullable>enable` is
  now the baseline (in Directory.Build.props) — **keep new code warning-free.**
- **HW-TEST DEBT unchanged/added:** the audio A/E rewrites live in the largely-untested capture/streaming
  runtime path → still need the maintainer's hardware test (desktop audio → real Chromecast, WAV+MP3, over time).

---

## 1. What KlangHub is
A Windows / .NET 10 WinForms app that captures the PC's audio and casts it to Chromecast devices (mid-rebrand
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

**NEW CHAPTER 2026-07-05 (commit `6dba09f`, HW-test PENDING): the group-hosting speaker — dedup by IP:port.**
the maintainer's network regrouped: the Enchant now *hosts* the "the multi-room group" multizone group, so its IPv4 (`.154`)
serves BOTH its own `:8009` receiver AND the group's `:32223` leader. `Devices.GetDevice` deduped placeholder-MAC
devices by **IP alone**, so the Enchant `.154:8009` matched the co-located group at `.154` → OnDeviceAvailable took
the *update* branch → `onAddDeviceCallback` never fired → no tile (log: `Discovered device: Enchant …:8009` present
but no `Device added:`). **NOT a Tier-2/net10 regression** — the discovery code is byte-identical since net8 (nullable
annotations only, verified); this is a latent edge case triggered by the new topology. **Fix:** dedup placeholder-MAC
devices by **IP:port** (`SamePlaceholderEndpoint`), matching `ChromecastDeviceId.From` which already keyed placeholder
MACs on IP:port — so list-dedup and session-id finally agree. Receivers are always stamped `Port=8009` (eureka
`SetDeviceInformation`), so receiver-vs-receiver dedup is unchanged; only the receiver-vs-group false-match is removed;
groups still dedup by Id. Adversarially verified (root cause traced end-to-end; `breaksSaga=false`,
`duplicateTileRisk=false`). **70 tests green (5 new in `DevicesTests`).** KEY INSIGHT extends the saga: the placeholder
MAC must be keyed on **IP:port in BOTH** `Devices.GetDevice` AND `ChromecastDeviceId.From` (previously GetDevice used
IP-only, which was coarser). **Known follow-ups, NOT bundled:** (a) `AddStreamingConnection` matches the send-socket by
IP only → if the Enchant + its group cast simultaneously at `.154`, the socket could bind to the wrong Device (pre-
existing; now reachable). (b) **True IPv6-only discovery is still unsupported** (`DiscoverDevices.OnServiceAdded` skips
no-IPv4 announcements) — the maintainer asked for "IPv6 as an alternative", but this session's Enchant HAS IPv4 so the dedup
fix suffices; real IPv6-only support = bracket the eureka URL `http://[addr]:8008` + IPv6 `:8009` TLS + preserve the
ULA scope-id `%8` + IPv6 remote-matching in the streaming listener — a separate, larger spike (raw IPv6 is what
originally broke the whole saga).

**NEXT CHAPTER 2026-07-05 (commits `ef3ac32`+`4a6b51d`, HW-test PENDING): the Enchant goes IPv6-only-flapping —
IPv4 RECOVERY.** In later logs the Enchant advertised its own `_googlecast` **IPv6-only** (`fd1a:…%8`) in some scans
(no IPv4 at all) → the IPv6-skip dropped it → no tile, and the IP:port dedup fix couldn't help (it never reached
discovery). BUT the Enchant is **dual-stack**: its `.154` IPv4 is *donated* by the "the multi-room group" group it hosts
(same `fd1a` IPv6 host, announced WITH `.154`). **Fix = `Ipv4Recovery`** (`Discover/Ipv4Recovery.cs`): learn each
device's IPv4 keyed by its mDNS `id=` AND by every IPv6 host announced alongside it (scope-normalized, 120s TTL);
an IPv6-only announcement then recovers a usable IPv4 → discovered + controlled + streamed **over IPv4**, no risky
IPv6 path. `DiscoverDevices` now does `primary = ipv4 ?? recovered` (never enqueues a raw IPv6 literal). Also added
(dormant foundation, IPv6-ready but not reached while every device is keyed on IPv4): bracketed+zone-stripped eureka
URL (`DeviceInformation.UrlHost`), scoped `:8009` connect (`DeviceConnection.BeginConnectTo` — the `WSAEADDRNOTAVAIL`
fix: clear the ULA `%zone`, keep link-local), and a scope-normalized streaming address-set match
(`DiscoveredDevice.Addresses` + `Device.MatchesAddress`/`MergeAddress`). **Adversarially verified — the review CAUGHT
TWO real risks, both fixed in `4a6b51d`:** R1 duplicate tile (raw IPv6 literal kept → a `[ipv6]:8009` tile that never
dedups; deterministic for the eureka-less TVs) → fixed by recover-or-skip; R2 stale-cache misroute under DHCP churn
→ fixed by the 120s TTL. `net8` gestern fand ihn zufällig (er hatte damals IPv4). **KEY: this did NOT regress the
net10/Tier-2 discovery** (byte-identical there); it's a NEW device-topology case (IPv6-only-flapping). 79 tests green
(9 new `Ipv4RecoveryTests` + `DevicesTests`). **True-IPv6-only (a device with NO IPv4 ever) remains unsupported** —
it's intentionally skipped (no audio return path over IPv6; the dormant plumbing above is the head-start for that
separate milestone). **HW-test focus:** Enchant tile appears + casts on itself over the recovered `.154`; the 2 TVs
never spawn a `[ipv6]` duplicate; the multi-room group still casts separately.

## 5. Roadmap (next)
- **M4 — Snapcast control-plane** (recommended next feature): `SnapcastProvider.CreateSession` → a real
  JSON-RPC 2.0 / WebSocket session to an **existing** snapserver (connect-to-existing per decision D2):
  `Server.GetStatus` + `Client.SetVolume` + `Group.SetStream` + `On*` events. Structure/discovery already
  exist. Snapserver doesn't run on Windows, so KlangHub connects to one on the LAN (Linux/Pi/Docker).
- **AirPlay streaming** — a large, dedicated later spike (transient HomeKit pairing SRP-6a + X25519/Ed25519 +
  ChaCha20 + ALAC/RTP; no usable managed .NET sender → port/interop the Apache-2.0 `airplay2-sender-cpp`).
  Discovery already works. Note: the Enchant/TCL/Soundbar are AirPlay2-capable too.
- **Housekeeping (non-blocking):** ~~net8→net10 LTS bump~~ **DONE 2026-07-05** (commit `098a9d5`, see §0);
  move Orchestrator/SettingsService to a `KlangHub.App.Core` lib so Tests needn't reference the WinExe;
  namespace tidy (IDevice/Device still `KlangHub.Application`); optional later: enable `<Nullable>` +
  audio hot-path optimizations (Tier B, deferred).

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
- SDK: `%USERPROFILE%\.dotnet\dotnet.exe` — now **10.0.301** (and 8.0.422), per-user, **not on PATH** (the
  `dotnet` on PATH is `C:\Program Files\dotnet` which has runtimes only, no SDK → build with the per-user exe).
- .NET 10 **runtimes** are installed both per-user (`~/.dotnet`, 10.0.9) **and machine-wide**
  (`C:\Program Files\dotnet`, NETCore.App + WindowsDesktop.App 10.0.9) — the latter is what lets the
  framework-dependent EXE launch on a plain double-click (the app host searches the machine-wide location, not
  `~/.dotnet`; a per-user-only runtime needs `DOTNET_ROOT` set or a self-contained publish).
- Build the **.sln** in Release (`dotnet build Source/KlangHub.sln -c Release`) — the legacy `Setup Project`
  vdproj is skipped by the CLI (harmless MSB4078 warning). Output:
  `Source/KlangHub/bin/Release/net10.0-windows/KlangHub.exe`.
- **Kill running `KlangHub.exe` before a Release build** — a running instance locks Core/Platform DLLs
  (MSB3021/3027).
- `dist/` is gitignored; zips are `dist/KlangHub-Release-<shorthash>.zip` (framework-dependent, ~83 MB).
- Commit messages via `git commit -F <file>` (PowerShell here-strings mangle multi-line `-m`; `<<` heredocs
  don't exist in PowerShell). The Bash tool's `Remove-Item` with a wildcard can be sandbox-blocked — delete
  zips by exact `-LiteralPath`.
- git core.autocrlf=true → "LF will be replaced by CRLF" warnings are harmless.

## 9. Premium Chromecast 2026 sprint (2026-07-05, commits `82ebb89`..`e186d7c`) — HW-TEST PENDING
Spec: `docs/superpowers/specs/2026-07-05-premium-chromecast-2026-design.md`. Derived from the two root PDFs
("Chromecast Audio Entwicklung 2026", "Robuste Chromecast-Sender-Implementierung … Stand 2026"; text extracted
via pdftotext/PyMuPDF — the "Robuste" PDF is image-only, rendered to PNGs and read visually). Scope locked with
the maintainer: **codec = both parallel** (WAV-16-bit lossless default now + real FLAC live-encoder), **TV = branded
full-screen artwork via the Default Media Receiver** (no custom animated receiver). 124 tests green, 0 source
warnings, app boot-smoke-tested on net10, artwork endpoint curl-verified over a real socket.

- **G1 — StreamCodec (Core) single source of truth** (`630f7f5`): `ContentType/IsWav/IsMp3/IsFlac/IsLossless`;
  `SupportedStreamFormat.Flac` appended last (stable ordinals). HTTP header AND Cast LOAD `contentType` now both
  route through it (was hardcoded `audio/wav` in BOTH places, even for MP3). New `ICastHost.GetStreamMediaInfo`;
  threaded via IDevice/Devices/DeviceCommunication. WAV byte-identical; MP3 now correctly `audio/mpeg`.
- **G3 — FLAC live-encoder** (`630f7f5`): pkg `CUETools.Codecs.FLAKE` (managed, **LGPL-3.0** → separate DLL +
  `docs/THIRD-PARTY-LICENSES.md`). `FlacEncoder : IAudioEncoder` → `FlakeWriter` into a
  `NonSeekableForwardingStream` (**CanSeek=false**, verified: FlakeWriter only patches STREAMINFO if seekable →
  `total_samples=0` endless progressive FLAC). `LoopbackCaptureEngine` forces **16-bit INT** for Flac (FLAC needs
  integer, not the float mix format). Combobox lists FLAC (recommended); resx `Flac`.
- **G4 — premium TV screen** (`c877a47`): LOAD sends **metadataType:3** (MusicTrackMediaMetadata) title/artist/
  albumName + artwork image URL, via neutral Core `CastMediaMetadata`. Streaming server routes `GET /artwork.png`
  (`ArtworkHttp`) to a **1280×1280 branded PNG** (embedded resource `KlangHub.Resources.artwork.png`, designed
  with frontend-design: warm-dark vignette + radiating amber sound-rings from a glowing hub node + tracked
  wordmark). resx `Media_Subtitle` (en+fr).
- **G5 — reconnect backoff** (`e186d7c`): pure/tested `BackoffPolicy` (Core; 1→2→4..cap, jitter, Reset-on-success).
  Wired **conservatively** into DeviceCommunication's "stuck launching" retry (base=5s → 5/10/20/30, never faster
  than the old flat 5s; Reset on PLAYING). Full ConnectionManager state-machine + error-state circuit-breaker
  DEFERRED (fragile comm path).
- **G6 + G2** (`e186d7c`): **RST-on-stop** (`StreamingConnection.Dispose` sets `LingerState(true,0)` → TCP RST,
  no keep-alive socket-junk). **Lossless out-of-box**: first-run + fallback default `Mp3_320` → **`Wav_16bit`**
  (guaranteed-working lossless; FLAC is the recommended premium option, flip default to Flac once HW-confirmed).

**FLAC de-risked as far as headless allows** (`af409bc`, 125 tests): a round-trip test decodes the encoder's
real frames back **byte-identical** (lossless proven; the live stream's `total_samples=0` is a streaming
property — a file decoder rejects it, a streaming receiver like Chromecast's Shaka/CAF does not), and a bench
measured **~440× real-time** encode at level 5 (never bottlenecks the capture thread). **Ready-to-test build:**
`dist/KlangHub-Release-af409bc.zip` (framework-dependent, ~7 MB, ships the FLAKE DLLs; runs on the machine-wide
net10 runtime).

**Enchant/IPv6 thread — cross-service IPv4 bridge (`9ade996`).** The safe, additive step the checkpoint §4
named and `2402f1d` instrumented for: `DiscoverDevices` now also browses `_airplay._tcp`/`_raop._tcp` PURELY to
feed `Ipv4Recovery` (id=null, host-keyed) — so when the Enchant flaps its `_googlecast` IPv6-only, its IPv4 is
recovered from the co-located AirPlay/RAOP service under the shared `fd1a…` host, and it gets a tile + streams
over IPv4. Makes NO tiles itself; recover-or-skip unchanged ⇒ cannot regress the working IPv4 path (inherits
R1/R2). **Efficacy HW-confirmable:** helps iff the AirPlay service carries an IPv4 with the shared host (common
for dual-stack AirPlay speakers); if it too is IPv6-only, the bridge is inert and the remaining path is the
truly-IPv6-only-everywhere milestone (dual-stack listener + `[v6]` URL), decided by the `mDNS-svc` logs. Look
for a `mDNS-bridge [_airplay/_raop] learned IPv4 … for hosts=[fd1a…]` line + the Enchant getting a tile.

**HW-TEST asks (the maintainer's acceptance — I have no devices):** (a) 5 devices appear (IPv4+IPv6) — incl. the Enchant
via the bridge above; (b) cast to a speaker over WAV-16-bit lossless AND FLAC; (c) **live progressive FLAC
actually plays** on a real receiver (the one unproven-on-hardware piece — WAV-16-bit is the fallback if a device
rejects the endless FLAC stream; then revisit 24-bit or libFLAC); (d) TV shows the branded artwork + title, not
the generic screen; (e) reconnect recovers after a Wi-Fi drop; (f) stop leaves no socket junk on the next play.
**DEFERRED (Sprint 2+):** Opus · adaptive buffer-monitoring (react to BUFFERING) · custom animated web receiver
(needs Cast app-id + hosting) · true IPv6-only audio path (dual-stack listener + `[v6]` URL) · `ca`-bitmask
device typing · full reconnect state-machine · 24-bit FLAC.

## 10. HW-test #1 follow-up (2026-07-05, commits `311241d`..`040da3f`) — 139 tests green
**HW-test #1 (build `9ade996`) result — mostly GREEN:** TV artwork looked great; 4 devices appeared immediately;
the Enchant appeared ~60s later on its own (**the cross-service IPv4 bridge worked** — `mDNS-bridge …learned
IPv4 192.168.1.156 …` in the log); all cast high-bitrate WAV. Two real problems surfaced in the log, analyzed +
adversarially verified via a workflow (21 agents, verified vs the placeholder-MAC saga) before fixing.

- **BUG A — DHCP-move zombie tile (`02862a9`):** the Enchant power-cycled and DHCP moved it `.154 → .156` (IPv4
  AND its `fd1a` IPv6 host both changed). A 2nd tile spawned for `.156` (streamed fine) while the old `.154`
  tile became a permanent zombie, hammering `ConnectError → ResumePlaying → two 5s blocking timeouts` every 15s
  for 8+ minutes. **Fix 1 (cure):** reconcile placeholder-MAC devices by the stable mDNS `id=` BEFORE the
  IP:port fallback — `SetDeviceInformation` DROPPED the id at the eureka boundary (root cause) → now plumbed
  through; `Devices.GetDevice` adds a guarded `SameStableId` match (only when incoming `id=` non-empty, scoped
  `!IsGroup`) so a moved device updates its tile instead of orphaning it. **Guarded so it cannot regress the
  HW-confirmed 5-device dedup** (distinct devices have distinct ids; no id= → today's IP:port; `SamePlaceholder
  Endpoint` + `ChromecastDeviceId.From` untouched). Eureka log now prints `id=` (instrument for next run —
  confirm Google TV/TCL each carry a distinct id). **Fix 2 (bounds the spam):** ConnectError circuit-breaker in
  `Device.OnGetStatus` — pure/tested `ShouldRunPoll` gate backs the ConnectError poll off 15→30→60s; healthy
  devices unaffected; control-plane only.
- **BUG B — "noise" on the Enchant after ~60s (`2c0bb54`):** high-bitrate LPCM (96 kHz) over flaky Wi-Fi →
  receiver-side underrun (adversarially verified: NOT a sender byte-bug — TCP delivers correctly or disconnects).
  **Mitigations shipped:** cap capture at **48 kHz** (Cast receivers force a 48 kHz mixer → 96 kHz is resampled
  away anyway; halves the on-wire bitrate 6→3 Mbit/s, no audible loss) + out-of-box **buffer 10s** (more receiver
  cushion). True codec resilience (Opus/FLAC per-device) remains the deferred real cure.
- **32-bit WAV verified correct (`2c0bb54`):** probe on this HW proved WASAPI shared-mode genuinely CONVERTS the
  32-bit-float mix to the requested int rate/depth (byte counts scale with bit depth) → the 32-bit-int WAV
  default is right; the analysis' "garbage-by-construction" concern did not hold. **Instrument-first paid off.**
- **the maintainer's requests:** **32-bit WAV** is the recommended out-of-box default (picker reordered, HW-confirmed the
  TVs+Soundbar play it — old "TVs LOAD_FAILED on WAV" did NOT reproduce); **buffer 10s**; **version 0.0.0.1**;
  **German language** (`040da3f`) — `Strings.de.resx` (86 strings), picker 3rd item, `SetCulture("de")`, and
  German is the first-run default when the OS is German (this dev machine is `de-DE`; the version bump changes
  the `user.config` path → a fresh first-run picks German + Wav_32bit + buffer 10).

## 11. HW-test #2 follow-up (2026-07-05, commits `a23a1b3`, `f4f9e6d`) — 140 tests green
**HW test #2 (build `040da3f`, 32-bit WAV / 10s buffer) result:** the Enchant appeared cleanly via the id
(`eureka: … id=b3f62d38…`; ids ARE present + distinct: Google TV `8d12b893`, TCL `46b3a763`, Soundbar
`b66d62a4`, Enchant `b3f62d38`) — the discovery/zombie work holds. BUT after ~45–70s the Enchant threw
`{"type":"ERROR","detailedErrorCode":102}` → IDLE(idleReason ERROR) → KlangHub reconnected + reloaded (the maintainer:
"Unterbrechung, dann neu verbunden"); the Soundbar degraded to noise. **detailedErrorCode 102 = receiver
media/OOM error** — the PDF's exact warning: **audio-only device + high-res UNCOMPRESSED LPCM → OOM.** With the
Default Media Receiver the receiver buffer can't be tuned, so the only lever is the codec.
- **FIX = FLAC as the out-of-box default (`f4f9e6d`, the maintainer's choice):** lossless HiFi like WAV but COMPRESSED
  (~half the byte rate + block-based) → doesn't OOM small speakers. Capture Flac 16-bit → **24-bit** (verified:
  FLAKE round-trips 24-bit byte-exact; +test). Picker: FLAC first (recommended); WAV 24/16/32-bit remain as
  uncompressed alternatives. Fresh-first-run smoke: boots with FLAC + 24-bit capture, no crash.
- **Zombie id-reconcile completed (`a23a1b3`):** the log showed the device's own eureka re-fetch (Flow 2) passes
  `id=null`, which re-Initialized the tile with an empty id (alternating `id=b3f62d38…` / `id=` lines) → would
  defeat the reconcile on a LATER move. Fix: preserve the tile's id when an incoming announcement carries none.

**Still HW-pending (test #3):** does **live 24-bit FLAC actually play cleanly on all 5 devices** (incl. the
Enchant — the whole point; WAV 24/16-bit is the fallback if a receiver rejects the endless FLAC stream)? German
UI on the German OS? zombie stays gone across a real DHCP move? DEFER: Opus (even more packet-loss-resilient)
per-device codec milestone; `WavGenerator.GetSilenceBytes` hardcodes 16-bit samples (wrong silence *duration*
for 24/32-bit, but byte-aligned so not the noise — minor latent bug, fix opportunistically).

> KlangHub (Windows/**.NET10** WinForms Chromecast audio caster). We're at commit `098a9d5` on master, 65 tests
> green, working tree clean. The full state is in `docs/CHECKPOINT-2026-07-04.md` (read it — esp. §0 for the
> net10 migration). Summary:
> 3-project split (Core/Platform/App) done; Orchestrator + SettingsService + CompositeCastProvider +
> IAudioSink seam + Task-based control all in place (phases H1–H4c, M1, M2, M3). The Chromecast
> discovery-robustness saga (the Harman Kardon "Enchant Speaker" multi-device network) is **resolved and
> hardware-confirmed** — the key was treating the placeholder MAC `00:00:00:00:00:00` as "no identity" in
> BOTH `Devices.GetDevice` (list dedup) AND `ChromecastDeviceId.From` (session id). Next logical feature is
> **M4: Snapcast control-plane** (connect to an existing snapserver via JSON-RPC/WebSocket); AirPlay
> streaming is a later dedicated spike. Please read the checkpoint doc, then propose a plan for the next
> step with honest risk analysis before implementing.
