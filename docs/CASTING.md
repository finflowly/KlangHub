# Casting

What happens between pressing play and hearing sound in another room. This describes the Chromecast
path as it exists in this repository; AirPlay and Snapcast are discovery only in 0.0.1 (see
[ARCHITECTURE.md](ARCHITECTURE.md)).

## 1. Finding the devices

`DiscoverDevices` browses `_googlecast._tcp` over mDNS (Tmds.MDns). Each announcement carries TXT
records — the friendly name (`fn`), the model (`md`), a stable id (`id`) — and a device's fuller
description is fetched from its `eureka_info` endpoint.

Three things in here look like details and are not:

- **The browsers must be held in a field.** They used to be locals; once the library stopped rooting
  them internally, discovery silently stopped and the room list stayed empty forever.
- **A device is identified by its stable mDNS `id`, not by its address.** A speaker that takes a new
  DHCP lease would otherwise leave a zombie card behind and appear a second time.
- **`_airplay._tcp` and `_raop._tcp` are browsed as well — but they create no room.** Some Cast
  built-in speakers occasionally announce `_googlecast` over IPv6 only, which is useless here because
  the stream is served over IPv4. Their co-located AirPlay announcement usually still carries an
  IPv4, and `Ipv4Recovery` bridges the two. A raw IPv6 address is never allowed into the device list:
  it breaks the `eureka_info` URL and the TLS handshake and produces a card that merges with nothing.

Groups are not guessed from a port number. `ProviderId` and `CastDeviceDescriptor.IsGroup` say what a
thing is.

## 2. Opening a session

The control connection is **TLS on port 8009**, carrying protobuf frames (`CastChannel.proto`, built
by `protoc` during the build — the `.proto` is the source, not a generated file checked in).

The device certificate is **not validated**. Cast devices present certificates from a chain no public
trust store knows; there is nothing to validate against. This is a documented property of the
protocol and one of the reasons KlangHub belongs on a home network rather than an open one — see
[SECURITY.md](../SECURITY.md).

Four Google namespaces carry the conversation:

```
urn:x-cast:com.google.cast.tp.connection    connect / close
urn:x-cast:com.google.cast.tp.heartbeat     PING / PONG
urn:x-cast:com.google.cast.receiver         LAUNCH, GET_STATUS, SET_VOLUME
urn:x-cast:com.google.cast.media            LOAD, PLAY, PAUSE, STOP, media status
```

`LAUNCH` starts a receiver application. With no application id configured that is Google's Default
Media Receiver; with one it is KlangHub's own (see below). The reply carries the transport id and the
session that everything afterwards is addressed to — and `CastReceiver.Matches` compares the id the
device *reports* against the id that was actually asked for. It used to compare against the
hard-coded default, so configuring a custom receiver launched it and then threw away the reply, and
nothing ever loaded.

Reconnection is governed by `ReconnectGate` and `BackoffPolicy` in Core, not by a retry loop in the
protocol code — which is why the pacing is covered by tests that run in milliseconds.

## 3. Serving the audio

Chromecast is a **pull** protocol (`DeliveryModel.PullHttp`). KlangHub does not push audio; it
listens on the selected IPv4 address on an ephemeral port and hands the device a URL, which the
device then fetches.

```
LoopbackCaptureEngine  →  IAudioSink[]  →  ChromecastAudioSink  →  IAudioEncoder
                                                                       │
                                             StreamingRequestsListener ┘  ← the device pulls
```

The encoder is chosen from the sound-quality setting: WAV 16/24/32-bit, FLAC (FLAKE) or MP3 128/320.
The `LOAD` message names the matching MIME type.

Two measured constraints shape the defaults:

- **Capture is capped at 48 kHz.** Cast receivers mix to 48 kHz anyway; recording higher only halves
  the headroom for nothing.
- **Uncompressed high-resolution audio can exceed what a small speaker can hold.** It plays for about
  a minute and then returns `detailedErrorCode: 102` followed by `IDLE(ERROR)`. That is the
  receiver's buffer, not the network. FLAC is the shipped default because it is lossless *and*
  compressed.

## 4. Saying what is playing

KlangHub does not own the music. It captures whatever Windows is playing, so it has to find out from
the outside what that is. `NowPlayingCascade` (Core) merges contributions from five sources into one
answer, per field, with the better source winning:

| Source | Where it lives |
|---|---|
| Windows now-playing session (SMTC) | `SystemMediaControlsSource` |
| Clementine's network remote | `ClementineRemoteSource` |
| File tags | `FileTagReader` (z440.atl.core) |
| Player window title | `WindowTitleSource` |
| File name | `FileNameGuess` (Core) |

The merge is a rule, not a call: it knows no file, no WinRT and no clock, and it is tested without
any of them. A listener casting from an external player sees a title at all only because these
decisions are made correctly.

The result travels two ways. The standard `LOAD` metadata carries title, subtitle, album and an
artwork URL. That artwork is served by KlangHub itself over the same local HTTP server — and the
request must carry the run's session secret. Without it, `/artwork.png` answered anybody on the
network who asked, with the cover of whatever was playing: a live account of what is being listened
to, handed to a guest on the wireless for nothing more than asking.

## 5. The television

For anything the standard metadata has no field for, there is a custom namespace:

```
urn:x-cast:de.klanghub.stage
```

It is used by the custom CAF v3 receiver in [`receiver/`](../receiver) — a separate web page served
over HTTPS, not part of the application. Audio never passes through it; the native CAF player handles
the stream exactly as it would with Google's default receiver, and the receiver only decides what the
screen shows.

To use it you need a hosted URL and an application id registered in the Cast Developer Console, then
*Settings → Cast receiver → App ID*. Leaving it empty keeps Google's Default Media Receiver and keeps
KlangHub fully offline. Registration, the test-device dance and the current status are in
[`receiver/README.md`](../receiver/README.md) — including the one known weakness that is not fixed
yet, and why it waits for the first run on real hardware.

## What this path is not

- It is **not synchronised** across rooms in the sense a multiroom product means. Several receivers
  buffer independently and drift. Real synchronisation needs a receiver we control and a latency
  offset per device; the plan is in [PLAN-MULTIROOM-SYNC.md](PLAN-MULTIROOM-SYNC.md) (German).
- It is **not lip-sync capable**. The buffer that keeps music playing makes video impossible.
- It is **not authenticated at the stream.** Cast does not authenticate senders and the audio server
  is plain HTTP on the LAN, as the protocol requires. [SECURITY.md](../SECURITY.md) states what that
  does and does not expose.
