# Getting started

From a clone to sound in another room. Everything here is taken from the code or from the shipped
user interface; nothing is described that you will not find.

## Build

You need the **.NET 10 SDK**. Every project targets `net10.0` or `net10.0-windows10.0.19041.0` — the
Windows SDK target is required for `Windows.Media.Control`, the now-playing session Windows keeps.

```
dotnet build Source/KlangHub.sln -c Release
dotnet test  Source/KlangHub.Tests/KlangHub.Tests.csproj
```

The test run is fast (seconds, not minutes) and is the normal way to check that a change is sound.
It includes `RepositoryPrivacyTests`, which walks everything git tracks — see
[CONTRIBUTING.md](../CONTRIBUTING.md) for why that matters here.

Visual Studio: open `Source/KlangHub.sln` and set `KlangHub` as the startup project.

An installer is a self-contained publish plus [Inno Setup](https://jrsoftware.org/isinfo.php):

```
dotnet publish Source/KlangHub/KlangHub.csproj -c Release -r win-x64 --self-contained true -o publish/KlangHub-0.0.1-win-x64
ISCC.exe installer/KlangHub.iss
```

> If `dotnet` on your `PATH` is a runtime without an SDK, the SDK is usually installed per user at
> `%USERPROFILE%\.dotnet\dotnet.exe`. Call that one.

## What the network has to allow

KlangHub is a local-network application. Three things happen on the wire:

| | Where | Why |
|---|---|---|
| **mDNS** (UDP 5353) | LAN, multicast | Finding devices. Without it the room list stays empty. |
| **Cast control** (TCP 8009, TLS) | LAN | Commands and status to and from the device. |
| **The audio stream** (TCP, ephemeral port) | LAN | KlangHub listens on the address you select; **the device pulls the stream from it**. |

Two consequences follow, and both are the usual reason a first attempt fails.

**Windows Firewall must let KlangHub accept incoming connections on your private network.** The
stream is not pushed to the speaker, it is fetched *from* KlangHub — so the speaker has to be able to
reach your PC, not the other way round. If the prompt at first start was dismissed, the room shows
*"Check firewall and Wi-Fi sharing"* and nothing plays.

**The PC and the speakers must be on the same subnet.** A guest network, a separate Wi-Fi SSID that
isolates clients, or a VPN adapter that captures the default route will all break discovery while
leaving the internet perfectly usable.

## First cast

1. **Start KlangHub.** It opens on the **Rooms** tab and begins looking for devices immediately.
2. **Check the address.** *Settings → Home network address (IPv4)* is the address the speakers will
   fetch the stream from. On a machine with several adapters — a VPN, Hyper-V, a second NIC — pick the
   one that is on the same network as the speakers. This is the single most common cause of a room
   that connects and then stays silent.
3. **Pick the audio source.** *Settings → Audio source* selects what is captured: the desktop output
   (loopback) or a microphone / line-in input.
4. **Choose the sound quality if you want to.** *Settings → Sound quality* offers WAV 16/24/32-bit,
   FLAC and MP3. A fresh installation streams FLAC with a ten-second buffer, which is the
   recommendation for most networks: lossless, and small enough to survive a wireless link that
   uncompressed 24-bit can overrun.
5. **Start a room.** Press play on a room card. The card reports its own state; the **Log** tab shows
   the conversation with the device.

If nothing was found at all, *Find rooms again* on the Rooms tab restarts discovery.

## When it does not work

**The room list stays empty.**
mDNS is not reaching the PC. Check that you are not on a guest network or an isolating SSID, that no
VPN adapter is in the way, and that the speaker is powered and visible in the Google Home app. A
device that announces itself only over IPv6 is recovered automatically — see
[BEKANNTE-FALLEN.md](BEKANNTE-FALLEN.md) — but one on a different subnet is not reachable at all.

**A room connects and then goes silent, or reports a firewall problem.**
The device could not open the stream. Almost always the firewall rule or the wrong *Home network
address*. Confirm the selected IPv4 is the one your speakers can reach.

**Playback stops after about a minute, and the log shows `ERROR 102`.**
That is the receiver's buffer, not the network. Small speakers can accept a high-resolution
uncompressed stream, play for a minute and then give up. Use FLAC or a lower WAV depth, and keep a
generous sound buffer. Measured on real hardware; the detail is in
[BEKANNTE-FALLEN.md](BEKANNTE-FALLEN.md).

**A room comes back from standby dead.**
Reconnection is deliberately paced — repeated failures are retried more and more slowly (15 → 30 →
60 s) so an unreachable device cannot flood the log. Give it a minute, or press play again.

**Two cards appear for one speaker, or a card refuses to die.**
A device that changed its DHCP address. Matching runs on the stable mDNS id and should heal on the
next announcement; *Find rooms again* forces it.

## Diagnosing more deeply

*Settings → Record a device log* writes the conversation with the devices to a file, and
`tools/analyse-device-log.ps1` summarises it.

**That log contains the addresses of every speaker in the house and what was played on them.** It is
covered by `.gitignore` and must never be attached to an issue or a security report as it is —
replace addresses and device names with example values first.

## Where to go next

- [ARCHITECTURE.md](ARCHITECTURE.md) — how the pieces fit together.
- [CASTING.md](CASTING.md) — what actually happens between pressing play and hearing sound.
- [BEKANNTE-FALLEN.md](BEKANNTE-FALLEN.md) — the mistakes that already cost hours. German.
