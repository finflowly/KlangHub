# M1 — Multi-Protocol Preparation: Technical Analysis & Design

Status: **design / analysis** (Phase M, sprint M1). No production code changes.

> Historical note: this analysis was written while the app still targeted .NET 8. The .NET 10 move
> recommended in §7 has since happened — every project now targets `net10.0` / `net10.0-windows10.0.19041.0`.
> The protocol analysis itself is unaffected.
Scope: prepare KlangHub (Windows / .NET 8, WinForms, Chromecast audio caster) so **AirPlay 2** and
**Snapcast** can be added later with bounded effort — decide the abstraction extensions, the structure,
and an honest, evidence-based roadmap. **Explicitly not** a protocol implementation.

Sources are listed per section; the deep research is archived in the workflow transcript.

---

## 0. Executive summary & feasibility matrix

| Target | Verdict | Why | First step |
|---|---|---|---|
| **AirPlay discovery** | 🟢 Easy | mDNS `_airplay._tcp` + `_raop._tcp`; Tmds.MDns already browses arbitrary types | Discover + classify receivers |
| **Legacy RAOP sender** (old/unauth `et=1` speakers) | 🟠 Hard | Documented; port node_airtunes/pyatv-RAOP; ALAC + AES-128-CBC, NTP timing | after discovery |
| **Full AirPlay 2 sender** (HomePod / modern Apple TV) | 🔴 Very hard | Transient HomeKit pairing (SRP-6a + X25519/Ed25519 + ChaCha20-Poly1305) + ALAC + RTP; **no usable managed lib** | prototype pairing vs 1 HomePod = go/no-go gate |
| **AirPlay 2 multi-room (PTP sync)** | 🔴 Very hard | IEEE-1588 PTP; realistically needs the native port | stretch goal, not v1 |
| **Snapcast control** (existing server) | 🟢 Easy, high value | Pure .NET JSON-RPC 2.0 over WS; volume/mute/group/stream | **the recommended first real non-Chromecast integration** |
| **Snapcast audio feed** | 🟠 Medium + constraint | WASAPI loopback → PCM → `tcp://` source; **snapserver doesn't run on Windows** (needs Linux/Pi/Docker/WSL) | defer to after control |

**Key make-or-break finding:** there is **no production-ready managed .NET AirPlay *sender***. The full
AirPlay 2 path means **porting / native-interop-wrapping** an existing implementation (best candidate:
`akustikrausch/airplay2-sender-cpp`, Apache-2.0), not wrapping a NuGet. So AirPlay 2 real streaming is a
large, dedicated later effort — **this phase keeps AirPlay to discovery + a pairing feasibility spike**.
Snapcast is the tractable, demonstrable win.

**.NET is the right platform** (Chromecast/NAudio/Tmds.MDns/xUnit ecosystem, WinUI-3 path); an eventual
**net8→net10** LTS bump is low-effort and **non-blocking** (see §7).

---

## 1. AirPlay 2 — analysis

**Discovery.** Receivers advertise `_airplay._tcp` (AirPlay 2 control) and `_raop._tcp` (legacy AirTunes
audio; instance name `<MAC>@<name>`). Decisive TXT keys: `features` (64-bit capability bitmask), `et`
(encryption types: 0=none, 1=RSA/AES, 3=FairPlay, 4=MFi-SAP), `cn` (codecs: PCM/ALAC/AAC/AAC-ELD), `pk`
(Ed25519 pubkey), `srcvers`, `model`. **Rule:** parse `et`/`features` to classify each receiver as
*legacy-RAOP-streamable* vs *AirPlay-2-pairing-required*.

**Pairing / auth (the crux).** Three regimes: (a) legacy RAOP `et=1` — sender-chosen AES key RSA-wrapped
with Apple's well-known public key, **no pairing**; (b) AirPlay 2 **transient HomeKit pairing** — SRP-6a
(3072-bit) with an implicit passphrase → 32-byte audio key, then X25519 ECDH → HKDF-SHA512 →
ChaCha20-Poly1305 control channel, Ed25519 identities, TLV8 + binary-plist wire format, **no user PIN**;
(c) pair-verify + on-screen PIN (persistent). **MFi/FairPlay is NOT required** for the common audio case —
unofficial senders (Cider, pyatv, the Apache C++ sender) stream to HomePod/Apple TV without it. The hard
part is the **undocumented sequencing**, not the crypto primitives (which .NET 8 has: X25519, Ed25519,
ChaCha20Poly1305, HKDF; SRP-6a/TLV8/bplist to implement or port).

**Audio.** AirPlay 2 realtime receivers demand **ALAC** (44.1 kHz/16-bit stereo, 352 frames/packet), over
**RTP/UDP**, payload **ChaCha20-Poly1305** (AP2) or AES-128-CBC (legacy). Multi-room sync = **PTP/IEEE-1588**
(the hardest piece; single-speaker uses simpler NTP-style timing). A session **dies after ~30 s** unless the
sender keeps answering the receiver's event-channel — keep-alive is a background concern.

**Control semantics (feeds the void→Task decision).** RTSP `OPTIONS/ANNOUNCE/SETUP/RECORD/SET_PARAMETER/
FLUSH/TEARDOWN` + pairing POSTs are **request/response and all can fail** → they want awaitable, error-
returning calls. Volume is a **dB float** (~−30…0, −144=mute), *not* 0–100. The RTP audio pump + timing/
sync are **fire-and-forget** background loops with a health signal, not per-call returns.

**.NET landscape.** No production managed sender. WinStream (C#) = abandoned MVP (proves discovery+capture
are trivial in C#). Best references: **`akustikrausch/airplay2-sender-cpp`** (Apache-2.0, verified AP2 ALAC
sender — port or P/Invoke candidate), `ciderapp/node_airtunes2` (AGPL — *reference only*), `pyatv` (Python
RAOP), `ejurgensen/pair_ap` (C pairing reference). Capture (NAudio WASAPI loopback) + mDNS = easy in .NET;
ALAC/RTP/RTSP = moderate managed code; **pairing + PTP = the hard/very-hard parts** → lean on the Apache C++.

Sources: [Cozzi AirPlay2 Internals](https://emanuelecozzi.net/docs/airplay2/), [openairplay spec](https://openairplay.github.io/airplay-spec/), [nto AirPlay](https://nto.github.io/AirPlay.html), [airplay2-sender-cpp](https://github.com/akustikrausch/airplay2-sender-cpp), [node_airtunes2](https://github.com/ciderapp/node_airtunes2), [pyatv #1059](https://github.com/postlund/pyatv/issues/1059), [pair_ap](https://github.com/ejurgensen/pair_ap), [WinStream](https://github.com/Bananz0/WinStream).

---

## 2. Snapcast — analysis

**Architecture — one server + N synced clients (NOT per-device casting).** A `snapserver` reads PCM from
configured **stream sources**, encodes (FLAC default / Opus / PCM), timestamps, and pushes to `snapclient`s
that play in <1 ms sync. **Stream** = one source; **Group** = clients playing one stream in lockstep;
per-client volume/mute/latency/name. Transport control belongs to the *source*, not the sink — a client is a
dumb, volume-adjustable speaker.

**Audio feed.** Stream sources: pipe/**tcp**/file/process/meta/alsa/jack/librespot/airplay. A Windows app
feeds via a **`tcp://ip:port?mode=server`** source (WASAPI loopback → raw interleaved LE PCM `44100:16:2` →
socket; the server encodes downstream). **Constraint:** **snapserver does not build/run on Windows** (only
`snapclient.exe`) → the server lives on Linux/Pi/Docker/WSL. So realistically KlangHub **connects to an
existing server**, not bundles one.

**Control API.** JSON-RPC 2.0 over raw TCP **1705** (ndjson) or HTTP/WebSocket **1780** (`/jsonrpc`). Methods:
`Server.GetStatus` (full tree = source of truth), `Client.SetVolume/SetLatency/SetName`, `Group.SetMute/
SetStream/SetClients/SetName`, `Stream.Control/AddStream/RemoveStream`. Live model via `On*` notifications
(`Client.OnVolumeChanged`, `Group.OnStreamChanged`, `Server.OnUpdate`, …). Discovery via mDNS `_snapcast._tcp`
+ host:port fallback. **All ops are async/fallible** (round-trips).

**.NET libs.** `justr.snapcast-api` (NuGet wrapper), `Snap.Net` (control client + a .NET snapclient port +
broadcast tool, targets .NET 10). Rolling our own JSON-RPC/WS client is cheap.

Sources: [snapcast repo](https://github.com/snapcast/snapcast), [control.md](https://github.com/snapcast/snapcast/blob/master/doc/json_rpc_api/control.md), [configuration.md](https://github.com/snapcast/snapcast/blob/master/doc/configuration.md), [no Windows server #1380](https://github.com/snapcast/snapcast/issues/1380), [Snap.Net](https://github.com/stijnvdb88/Snap.Net), [justr.snapcast-api](https://www.nuget.org/packages/justr.snapcast-api).

---

## 3. Abstraction gap analysis — where the current model breaks

The abstraction grew around **Chromecast = PULL** (KlangHub is an HTTP server; the device pulls the stream;
`ICastHost.GetStreamingUrl`). Three hidden assumptions break:

| # | Assumption today | AirPlay | Snapcast | Fix |
|---|---|---|---|---|
| A | **App serves HTTP; device pulls** | App **pushes RTP** per receiver | App **feeds one TCP stream** to a server | **Neutral audio-sink seam** (below) |
| B | **1 session = 1 audio consumer**; audio fan-out hardwired to Chromecast `IDevices` | 1 session = 1 receiver (push) | **1 stream feeds N clients**; clients are control-only | Sink is per-**active-playback**, not per-device; audio path decoupled from `IDevices` |
| C | **Control is sync `void`** (fire-and-forget) | RTSP round-trips, **fallible** | JSON-RPC round-trips, **fallible** | **void→Task decision** (below) |
| — | **No pairing/auth concept** | AirPlay 2 needs transient pairing + persisted creds | none | Capability flag + pairing hook |

### Required extensions

1. **`IAudioSink` seam (the core change).** A neutral "consume this `AudioFrame`/PCM chunk" contract. The
   Orchestrator fans captured audio to **all active sinks** across providers, instead of the current
   hardwired `devices.OnRecordingDataAvailable`. Each provider implements delivery behind the sink:
   Chromecast → write to the device's HTTP connection (as today); AirPlay → ALAC-encode + RTP-push to the
   receiver; Snapcast → write raw PCM to the snapserver TCP source (**one sink regardless of client count**).
   *This is the behaviour-sensitive refactor* — Chromecast must stay byte-identical (H-phase discipline +
   the H4c tests + the HW gate).
2. **`CastProviderCapabilities` extension:** add `DeliveryModel { PullHttp, PushRtp, ServerFed }`,
   `RequiresPairing`, and (optionally) a preferred-codec hint. Lets the composition/audio path branch
   neutrally.
3. **Control async — recommend `Task`-returning control methods** (`Connect/Play/Pause/Stop/SetVolume/
   SetMuted/RequestStatus`). Evidence: **both** AirPlay (RTSP) and Snapcast (JSON-RPC) control ops are
   network round-trips that genuinely fail and where the caller wants success/failure (e.g. a rejected
   `SETUP`, an unknown Snapcast client id, a pairing failure). Chromecast wraps its sync ops in
   `Task.CompletedTask`. The **audio pump stays a background loop** (not per-call). *Cost:* ripples through
   `IPlaybackSession`/`Device`/REST/Tray/Orchestrator — do it once, cleanly, in M2. *(Final call in M2, per
   your instruction; the research leans Task for control.)*
4. **Snapcast shape:** model **server = provider/target**, **clients + groups = controllable child
   endpoints** (volume/mute/latency, group→stream assignment). `IPlaybackSession` mostly fits — `SetVolume/
   SetMuted` map to a client; "Play/Stop" for Snapcast = assign/unassign the group to the fed stream.
   Do **not** model each snapclient as a Chromecast-style "cast to" descriptor.
5. **Pairing hook (AirPlay-only, prep now / impl later):** an optional `PairAsync(descriptor)` on the
   provider (or inside `CreateSession`) + persisted per-receiver credentials. Gated by
   `Capabilities.RequiresPairing`. The actual handshake is the hard later effort.
6. **Encoder factory:** replace `new Mp3Encoder(...)` with an `IAudioEncoder` registry/factory. New impls
   later: **Opus** (Concentus, pure-managed), **FLAC** (NAudio/MediaFoundation or FlacBox), **ALAC**
   (LibALAC native — ALAC.NET is decode-only), **AAC-LC** (MediaFoundation). `AudioFormat` (PCM triple)
   stays sufficient.
7. **mDNS:** **Tmds.MDns already browses arbitrary service types** (`StartBrowse(IEnumerable<string>)`, no
   validation) → reuse it for `_airplay._tcp`/`_raop._tcp`/`_snapcast._tcp`. Switch to net-mdns/Makaretu
   only if we later need to **advertise** KlangHub or a meta-query (not now).

---

## 4. Structural preparation (Platform)

```
KlangHub.Platform/Casting/
├── Chromecast/     (existing)
├── AirPlay/        AirPlayProvider, AirPlayDiscovery   [later: Pairing/, Rtsp/, Rtp/, AlacEncoder]
├── Snapcast/       SnapcastProvider, SnapcastControlClient (JSON-RPC/WS), SnapcastStreamFeeder [later]
└── Shared/         MdnsDiscovery (generalized from ChromecastDeviceDiscovery), AudioEncoderFactory,
                    [later] RtspClient / JsonRpcClient helpers
```
Preparable now, provider-agnostic: (1) a **generic mDNS discovery helper** (lift the Tmds.MDns usage in
`ChromecastDeviceDiscovery` to a service-type parameter); (2) an **`IAudioEncoder` factory**; (3) the
**`IAudioSink` seam** (§3.1). The `Shared/` layer is what makes a 2nd/3rd provider "fill in the session,"
not "rebuild the core."

---

## 5. Roadmap & sequencing

Prepare **both** discoveries (the abstraction serves both); implement **Snapcast first** (tractable,
demonstrable), keep **AirPlay discovery-first** and gate its streaming behind a pairing spike.

| Sprint | Content | Risk |
|---|---|---|
| **M1** (this) | Research + this design doc | none |
| **M2** | `IAudioSink` seam + `CastProviderCapabilities` extension + **void→Task decision & refactor** — Chromecast-behaviour-neutral, with tests (reuse H4c) | med (touches core audio path + control ripple) |
| **M3** | `Casting/{AirPlay,Snapcast,Shared}`; generic mDNS helper; encoder factory; provider **shells** → **discovery works for AirPlay + Snapcast** (endpoints appear in the device list, classified) | low |
| **M4** | **Snapcast control-plane** vs an existing snapserver (JSON-RPC/WS: list clients/groups, volume/mute, group↔stream) — first real non-Chromecast integration; optionally the TCP audio feed | low–med |
| **(later, dedicated)** | AirPlay **pairing spike** vs one HomePod = go/no-go gate → then legacy-RAOP sender or port/interop the Apache-2.0 AP2 sender; PTP multi-room is a stretch goal | high |

---

## 6. Risks, scope control, open decisions

**Risks:** (1) AirPlay pairing/PTP crypto — no managed lib → port/interop; biggest unknown, gate early.
(2) The `IAudioSink` refactor touches the **live Chromecast audio path** — behaviour-preservation is
mandatory (tests + HW gate). (3) void→Task ripple. (4) Snapcast's Windows-no-server constraint limits the
feed to "existing server on the LAN."

**Explicitly NOT this phase:** a full AirPlay 2 encrypted sender; AirPlay pairing implementation; a bundled
snapserver; end-to-end "plays on real AirPlay speakers." Guard against sinking the phase into AirPlay crypto.

**Open decisions for review (before M2):**
- **D1 — void→Task for `IPlaybackSession` control?** Recommendation: **yes, for control ops** (both new
  protocols are async/fallible); audio pump stays background. Decide at M2 start.
- **D2 — Snapcast scope:** control an **existing** server first (recommended), defer the feed; bundling a
  server (WSL/Docker) is a later product decision.
- **D3 — AirPlay strategy:** discovery + a **pairing spike** now; commit to legacy-RAOP-only vs
  porting the Apache-2.0 C++ sender **only after** the spike.
- **D4 — Licensing:** `node_airtunes2` is **AGPL** (reference only, do not copy). `airplay2-sender-cpp` is
  **Apache-2.0** (port/interop OK). FDK-AAC (AAC-ELD) licensing to review if ever needed.

---

## 7. Side note — .NET LTS (non-blocking)

.NET 10 is the next **LTS** (Nov 2025 → supported to Nov 2028). **.NET 8 reaches end of support 2026-11-10**
(~4 months out) → plan a bump this year. **net8.0-windows → net10.0-windows** for this WinForms + NAudio app
is **low-effort** (bump `<TargetFramework>` + regression test; NAudio runs on net10; .NET 10 breaking changes
are overwhelmingly server-side). Schedule as its own small sprint, independent of Phase M.

Sources: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [.NET 8/9 EOL Nov 2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/), [Tmds.MDns](https://github.com/tmds/Tmds.MDns), [Concentus](https://github.com/lostromb/concentus), [LibALAC](https://github.com/GiteKat/LibALAC).

---

## 8. Expected benefit of Phase M

The casting abstraction becomes **provably** multi-protocol (an `IAudioSink` seam + pairing/async hooks +
capability model — not just the `CompositeCastProvider` placeholder from H4a). Visible progress: **AirPlay +
Snapcast discovery**, and a **working Snapcast control plane** (real multi-room control) as the first
non-Chromecast integration. The hard AirPlay work is isolated, honestly costed, and gated — the remaining
provider work becomes well-scoped "implement the session" jobs. This moves KlangHub toward "premium Windows
multiroom (Chromecast + AirPlay-aware + Snapcast)".
