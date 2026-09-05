# Architecture

How KlangHub is put together, and why. Written against the code as it stands in 0.0.1 — every type
named here exists, and the places where the shape is still unfinished are named as such.

## Three projects, one rule

```
Source/
  KlangHub.Core/       net10.0                        contracts and rules, no Windows
  KlangHub.Platform/   net10.0-windows10.0.19041.0    Chromecast, audio, Windows services
  KlangHub/            net10.0-windows10.0.19041.0    WinForms shell and composition
  KlangHub.Tests/      net10.0-windows10.0.19041.0    734 tests over all three
```

The rule that decides where a file goes is a single question: **does it need Windows to be true?**

`NowPlayingCascade` decides which of five metadata sources wins for a given field. That decision is
right or wrong regardless of the operating system, so it lives in Core and is tested without a
window, a socket or a speaker. `SystemMediaControlsSource`, which asks the Windows now-playing
session what it knows, needs `Windows.Media.Control` and therefore lives in Platform.

Core references nothing. Platform references Core. The app references both. There is no path back:
the Platform layer never learns that a WinForms application exists.

### Why Core is kept free of Windows

Not for portability — KlangHub is a Windows application and is not pretending otherwise. It is for
testability and for the lifetime of the rules. The interesting decisions in this project are rules,
not calls: which metadata source wins, when a reconnect is allowed, how a volume ceiling maps onto a
raw device level, what a discovery burst is allowed to cost. `ReconnectGate`, `BackoffPolicy`,
`DiscoveryThrottle`, `VolumeLevel`, `AudioRingBuffer` and `NowPlayingCascade` are all in Core, and
all of them are covered by tests that run in milliseconds and need no hardware. A rule that can only
be exercised by casting to a real speaker is a rule that stops being exercised.

## The casting seam

A protocol is not a branch in the app. It is a provider.

```
                    App (WinForms shell, Orchestrator)
                              │
                     ICastProvider  ◄── the only thing the app knows about
                              │
                    CompositeCastProvider
             ┌────────────────┼────────────────┐
             │                │                │
     ChromecastProvider  SnapcastProvider  AirPlayProvider
       (full session)    (discovery only)  (discovery only)
             │
      IPlaybackSession  ──► play / pause / stop / volume / mute / status
```

Five contracts in `KlangHub.Core/Core/Casting` carry the whole thing:

| Type | What it is for |
|---|---|
| `ICastProvider` | One protocol as a swappable peer. Owns its discovery, creates sessions. |
| `IDeviceDiscovery` | How that protocol finds its endpoints — mDNS today, an account listing in principle. |
| `IPlaybackSession` | Control of one endpoint. Provider-neutral: no protobuf, no TLS, no NAudio, no UI type crosses it. |
| `CastDeviceDescriptor` | Neutral identity of an endpoint: id, name, provider, group flag, optional model and facts. |
| `CastProviderCapabilities` | What the provider can do, and — through `DeliveryModel` — how audio reaches it. |

`ProviderId` replaced the heuristics this codebase used to run on. A Google Cast *group* was
recognised by its port not being 8009. That works until it does not, and it says nothing about what
kind of thing you are holding. A provider now says who it is.

### Why a composite and not a list

`CompositeCastProvider` is itself an `ICastProvider`. It fronts N real providers, routes
`CreateSession` by `CastDeviceDescriptor.Provider`, and merges every provider's discovery stream into
one event. The app therefore holds exactly one `ICastProvider` — the same shape it held when
Chromecast was the only thing that existed. Adding AirPlay and Snapcast to `Program.cs` was a change
to one constructor call and to nothing else.

That is the point of the seam. A second protocol should cost a class and a line in the composition
root, not an `if` in every place that touches a device.

### What a provider must not leak

`IPlaybackSession` returns `Task` from its commands even though the Chromecast implementation is
synchronous and returns an already-completed task. A network provider that has to await an
acknowledgement should not have to change the interface to do it.

Session ownership is deliberately provider-defined, and this is the sharpest edge in the seam. The
Chromecast provider returns a **non-owning view** over a live device that the registry owns; its
`Dispose()` is a no-op. Disposing a session must never tear down a shared endpoint — a provider that
hands out owned sessions and one that hands out views both satisfy the contract, and the XML doc on
the interface says which it is.

### `ICastHost`: the inversion that keeps Platform clean

A Chromecast session needs to know the stream URL, the stream title, the media metadata for the LOAD
message and whether auto-restart is on. All four are the application's business. Rather than let
Platform reference the app, the app implements `ICastHost` (a Core interface) and passes itself in.
`ApplicationLogic` implements it; `Orchestrator` provides the surface.

## The audio path

```
IAudioCaptureEngine        LoopbackCaptureEngine (WASAPI, NAudio)
        │  raw PCM
        ▼
   IAudioSink[]            fan-out — one sink today
        │
        ▼
   IAudioEncoder           WAV 16/24/32 · FLAC (FLAKE) · MP3 128/320
        │
        ▼
 StreamingRequestsListener local HTTP; the device pulls  (DeliveryModel.PullHttp)
```

Capture, encoding and delivery are three seams, not one method. `ChromecastAudioSink` owns the
encoder and the lag state and pushes into the device registry, which serves the bytes over HTTP.
`DeliveryModel` is what connects the two halves: `PullHttp` for Chromecast, `PushRtp` for AirPlay
(the app would push encrypted RTP instead of serving a URL), `ServerFed` for Snapcast (one PCM
stream into a snapserver, which fans out to its own synced clients). The enum exists so that the
day an AirPlay sink lands, the difference is already named and the sink array is already plural.

The encoder is guarded by a lock even though the capture engine is now built so that only one thread
can reach it. FLAKE reaches into its buffers through unsafe pointers; entering it twice does not
garble a stream, it corrupts the GC heap. The comment on `ChromecastAudioSink.encoderSync` records
the crash that put it there.

## The TV stage

The receiver is a **CAF v3 custom web receiver** in `receiver/` — plain HTML, CSS and JavaScript,
hosted over HTTPS and loaded by the Cast device from the internet. It is not compiled into the
WinForms application and must not be.

Audio never passes through it. The native CAF player handles the stream exactly as it would with
Google's default receiver; the custom receiver only decides what the screen shows. Metadata reaches
it two ways: the standard `LOAD` metadata, and a custom namespace, `urn:x-cast:de.klanghub.stage`,
for stage updates that the standard message has no field for.

The separation is not stylistic. A receiver is a web page under Google's control flow — registered
against a Google Cast developer account, served from a URL, updated by republishing that URL. An
application binary has none of those properties. Mixing them would mean shipping a new installer to
change a headline on a television.

**Status:** the receiver is complete in the repository and has **never run on real hardware**.
GitHub Pages is not switched on and no application id is configured. Its one known unfixed
weakness — stage messages carry no sender identity — is written down in
[`receiver/README.md`](../receiver/README.md), together with why the fix waits for the first run on
a real device rather than shipping untested.

## Composition

`Program.cs` is the composition root and the only place where concrete types meet. It builds the
logger, the device registry, the discoveries, the three providers, the composite, the orchestrator
and the form, in that order, and hands each one its dependencies. There is no container. For a
graph this size a container would hide the wiring without removing any of it.

## What 0.0.1 deliberately does not abstract

Naming this is more useful than pretending the seam is finished.

- **`Orchestrator` lives in the app project**, not in Core. It is free of WinForms and is tested
  without it, but `IDevices` transitively references the Platform `IDevice`, so it cannot move yet.
- **The device list in the shell is still Chromecast-shaped.** Discovery is neutral and
  `CastDeviceDescriptor` flows through the composite, but the tray list and the room cards are built
  on the Chromecast `IDevice`. Endpoints from other providers are surfaced in the log only.
- **AirPlay and Snapcast have no sessions.** Both providers discover and classify their endpoints and
  both throw `NotSupportedException` from `CreateSession`. AirPlay needs HomeKit pairing (SRP-6a,
  X25519/Ed25519, ChaCha20) and an ALAC/RTP sender; Snapcast needs a snapserver feed and JSON-RPC
  control. Neither is faked, and neither is claimed.
- **There is one audio sink.** The array is plural and the seam is real, but nothing has yet been
  written that proves it holds two.

The honest summary: the contracts are in place and carry a second protocol, the *user interface*
does not yet know that a second protocol could exist.

## Further reading

- [`M1-multi-protocol-design.md`](M1-multi-protocol-design.md) — the analysis behind the seam, with
  an unflattering effort estimate for AirPlay and Snapcast. English.
- [`PLAN-MULTIROOM-SYNC.md`](PLAN-MULTIROOM-SYNC.md) — why several receivers drift apart and what
  real synchronisation would require. German.
- [`GERAETE-INFORMATIONEN.md`](GERAETE-INFORMATIONEN.md) — what a Cast device actually reveals about
  itself, measured rather than assumed. German.
- [`BEKANNTE-FALLEN.md`](BEKANNTE-FALLEN.md) — mistakes that have already cost hours, with the
  symptom that identifies each one. German.
