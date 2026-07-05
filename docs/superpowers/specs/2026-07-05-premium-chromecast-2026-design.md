# Premium Chromecast 2026 — Design Spec

**Date:** 2026-07-05 · **Author:** Claude + the maintainer · **Base:** HEAD `2402f1d`, master, 79 tests green
**Source of truth:** the two root PDFs — *"Chromecast Audio Entwicklung 2026"* and *"Robuste
Chromecast-Sender-Implementierung (Google Cast Client) — Stand 2026"* (extracted to the scratchpad).
**Goal:** turn KlangHub into a premium, whole-home Chromecast caster — lossless out-of-the-box, a beautiful
TV screen during playback, and hardened connection behaviour, exactly as the PDFs prescribe.

This spec covers **Sprint 1**. Sprint 2+ items (Opus, adaptive buffer monitoring, custom animated receiver,
true IPv6-only) are listed at the end as explicitly deferred.

---

## 0. Decisions locked with the maintainer (2026-07-05)

- **Codec: both in parallel.** Ship WAV-16-bit lossless as the out-of-box default *now*, **and** add a real
  FLAC live-encoder this sprint. FLAC ships behind the maintainer's hardware audio test; WAV-16-bit is the
  guaranteed-working lossless net.
- **TV visual: branded full-screen cover via the Default Media Receiver** (`CC1AD845`). Rich
  `MusicTrackMediaMetadata` + a designed full-bleed artwork image served by our own HTTP server. **No** custom
  animated receiver this sprint (that needs a registered Cast app-id + hosting — deferred).

## 1. What is already PDF-conformant (do not touch)

Verified in code — these already match the PDFs and stay as-is:

- TLS on `:8009` with certificate bypass (`DeviceConnection.DontValidateServerCertificate`) — PDF §3.
- CastV2 framing (4-byte big-endian length + protobuf `CastMessage`), `CONNECT` to `receiver-0`, `PING`/`PONG`
  heartbeat, `LAUNCH` `CC1AD845`, cooperative `GET_STATUS` → `transportId` → `CONNECT` → `LOAD` — PDF §3/§5.
- `mediaSessionId` is read dynamically from every `MEDIA_STATUS` (`DeviceCommunication.OnReceiveMediaStatus`) —
  PDF's critical "don't send static control commands" rule.
- Discovery dedup never keys on IP alone; placeholder-MAC keyed on IP:port; `Ipv4Recovery` for IPv6-only
  flapping; numeric IPv4 in the stream URL (`http://ip:port/`) — PDF §1/§2 + the DNS-hardcode workaround.

## 2. Gaps this sprint closes

| # | Gap (PDF requirement) | Current | Change |
|---|---|---|---|
| G1 | Codec-correct `Content-Type` | hardcoded `audio/wav` in HTTP header **and** LOAD | codec-aware, single source of truth |
| G2 | Lossless out-of-box | first-run default `Mp3_320` | first-run default `Wav_16bit` |
| G3 | FLAC (best HiFi) | none | real FLAC live-encoder (`Flac` format) |
| G4 | Rich metadata → nice TV | title only, `metadataType:0`, empty images | `metadataType:3` + title/artist/album + full-bleed artwork |
| G5 | Reconnect backoff+jitter | fixed `Task.Delay`, ad-hoc | shared exponential-backoff-with-jitter policy at the existing reconnect points |
| G6 | RST on stop (flush socket junk) | graceful FIN (`Socket.Close`) | `LingerState(true,0)` → TCP RST on teardown |

## 3. Architecture

### 3.1 Codec as a single source of truth — `StreamCodec`

New neutral helper in `KlangHub.Core` (next to `SupportedStreamFormat`):

```
static class StreamCodec
    static string ContentType(SupportedStreamFormat f)   // audio/wav | audio/mpeg | audio/flac
    static bool   IsWav(f)  IsMp3(f)  IsFlac(f)
    static bool   IsLossless(f)                           // Wav* and Flac
```

`SupportedStreamFormat` gains one member: `Flac`. Every place that currently branches on the format
(`ChromecastAudioSink`, `StreamingConnection`, `ChromeCastMessages.GetLoadMessage`, `ApplicationBuffer`)
routes through `StreamCodec` so the HTTP `Content-Type`, the LOAD `contentType`, and the encoder selection can
never disagree again (root cause of G1).

### 3.2 FLAC live-encoder — `FlacEncoder : IAudioEncoder`

- Dependency: **`CUETools.Codecs.FLAKE`** (pure-managed, netstandard2.0 → net10-compatible; NuGet 1.0.5).
  **License: LGPL-3.0** — ship as a *separate* DLL (framework-dependent build already does), add the LGPL
  notice + attribution to the repo's third-party licenses. No native binary (unlike libFLAC), so the whole
  FLAC path builds and unit-tests headless.
- Live-stream mechanism (verified against FLAKE source): `FlakeWriter(null, forwardingStream, pcmConfig)`.
  FlakeWriter only seeks the output **`if (_IO.CanSeek)`**. We pass a `NonSeekableForwardingStream`
  (`CanSeek == false`) whose `Write(...)` pushes bytes into the existing streaming buffer. Result: FlakeWriter
  emits the FLAC `STREAMINFO` header on first write, then self-contained FLAC frames sequentially, with
  `total_samples = 0` (unknown) and MD5 unset — a valid, endless progressive FLAC stream. It never seeks, so
  nothing patches the header retroactively.
- The encoder emits its **own** header, so the `AudioHeader` RIFF/MP3 prepend path is skipped for FLAC.
- `IAudioEncoder.Encode(pcmBytes)` converts interleaved PCM bytes → `AudioBuffer` (respecting
  bits/channels/rate) and calls `FlakeWriter.Write`. `CompressionLevel` default **5** (real-time-safe on a
  desktop; configurable). `Read()` is a no-op — FLAC bytes are pushed straight through the forwarding stream
  into the send buffer, matching the pull-HTTP model.
- **Honest risk (HW-gated):** live progressive FLAC to a real Default Media Receiver is less common than
  file-based FLAC. If a device won't play the endless stream, WAV-16-bit is the fallback and we revisit
  (libFLAC/native or receiver-side handling). the maintainer's HW test is the gate.

### 3.3 Premium TV screen — metadata + artwork

- Extend the LOAD DTOs (`Metadata`, `Media`, `Image` in `ChromeCastClasses.cs`): `metadataType = 3`
  (`MusicTrackMediaMetadata`), `title`, `artist`, `albumName`, and `images = [{ url, width, height }]`.
  Title/artist come from settings (`StreamTitle`) + a sensible "KlangHub" default; the artwork URL points at
  our own server.
- **Artwork endpoint.** The streaming HTTP server currently treats *any* request with a complete header block
  as an audio pull. Change: parse the request line. `GET /artwork.*` → respond `200` with the branded image
  bytes (`Content-Type: image/png`, `Content-Length`, `Connection: close`) and close. Anything else → the
  existing audio stream. LOAD's `images[0].url` = `http://<senderip>:<port>/artwork.png`.
- **The image.** A designed, full-bleed 1280×1280 "KlangHub — Now Playing" cover (dark, premium; wordmark +
  subtle audio motif), created with the `frontend-design` skill (authored as SVG → rasterised to PNG),
  embedded as an app resource. The Default Media Receiver renders it full-screen and overlays the
  title/artist text → premium look, no generic "Default Media Receiver" screen.

### 3.4 Reconnect hardening — `BackoffPolicy`

- New small, pure, unit-testable `BackoffPolicy` in `KlangHub.Core`: `1s → 2s → 4s … capped 30s`, ±0.5s
  jitter, `Reset()` on success (formula straight from the PDF's C++ sample). Deterministic in tests via an
  injectable jitter source.
- Applied at the existing reconnect decision points in `DeviceCommunication` (the `userMode == Playing`
  resume loop) — replacing fixed `Task.Delay(2000/5000)` with policy-driven waits. **Scope is honest:** we do
  *not* rewrite the whole comm layer into a new `ConnectionManager` state machine this sprint (that path is
  fragile and HW-sensitive per the checkpoint); we improve the *timing* and add clear
  `Disconnected/Connecting/Connected/Reconnecting` state tracking around what exists.

### 3.5 Socket teardown — RST

`StreamingConnection.Dispose` (and stop teardown) sets `Socket.LingerState = new LingerOption(true, 0)` before
`Close()` so the OS sends a TCP RST, flushing unread bytes — the PDF's fix for "socket junk" being misread as
the next song's HTTP headers on keep-alive.

### 3.6 First-run auto-config

First-run defaults (`ApplicationLogic` first-run path + the settings fallbacks): `StreamFormat = Wav_16bit`
(lossless), `ExtraBufferInSeconds` kept at the sane home default. The stream-format combo box surfaces **FLAC
(recommended, HiFi)** prominently. Once the maintainer's HW test confirms live FLAC, we flip the first-run default to
`Flac` (one-line change, tracked as a follow-up).

## 4. Testing

- **Unit (headless, I verify):** `StreamCodec` content-types/classification; `FlacEncoder` produces a valid
  FLAC stream from PCM (magic `fLaC` + a decodable first frame, decoded back via FLAKE's reader);
  `BackoffPolicy` sequence + cap + jitter bounds + reset; LOAD JSON carries `metadataType:3` + artist/album +
  image url; artwork request routing (path parse → image vs audio); existing `ChromecastAudioSinkTests` /
  `DevicesTests` stay green (WAV/MP3 byte-identical).
- **Build + smoke (I verify):** Release build 0 warnings; app launches on net10; discovery runs; artwork
  endpoint returns the PNG (curl the local URL).
- **Hardware (the maintainer verifies):** 5 devices appear (IPv4 + IPv6); cast to a speaker over lossless WAV *and*
  FLAC; cast to a TV shows the branded full-screen artwork + title, not the generic screen; reconnect recovers
  after Wi-Fi drop; stop leaves no socket junk on the next play.

## 5. Risks (kein reines Lob)

1. **Live FLAC on real receivers is unproven** — mitigated by the WAV-16-bit default net; HW-gated (§3.2).
2. **LGPL-3.0 (FLAKE)** — compliant as a separate, replaceable DLL + attribution; flagged because the maintainer
   tracks licensing (checkpoint §6/D4). Native libFLAC (BSD) is the alt if LGPL is unacceptable — but it needs
   a native binary I can't build/verify headless here.
3. **Real-time FLAC encode cost** — level 5 stereo 48k is far faster than real-time on a desktop; configurable
   down if a weak CPU shows underruns. The audio hot-path is the fragile, largely-untested area (checkpoint) →
   HW test is essential.
4. **Artwork reachability** — the Chromecast must fetch `http://<senderip>:<port>/artwork.png`; same host/port
   already proven reachable for audio, so low risk, but firewall-dependent like the audio stream.
5. **I cannot run the 5-device/TV smoke test** — no hardware. I deliver build + unit + local-endpoint smoke;
   the device/TV acceptance is the maintainer's.

## 6. Deferred (Sprint 2+)

Opus encoder · adaptive buffer-monitoring (react to `BUFFERING` MediaStatus) · custom animated web receiver
(needs Cast app-id + hosting) · true IPv6-only audio path (dual-stack listener + `[v6]` stream URL) ·
`ca`-bitmask device typing · full `ConnectionManager` state-machine rewrite.
