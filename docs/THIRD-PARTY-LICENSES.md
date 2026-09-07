# Third-Party Licenses

KlangHub is distributed under the MIT License (see [LICENSE](../LICENSE)). This document lists every
third-party component it uses, the licence each is under, and — where a licence asks for more than a
mention — how that obligation is met.

Components ship as their own assemblies. The published build is **self-contained** — it carries the
.NET runtime with it — but it is **not** single-file and **not** trimmed: every component listed below
lands in the installation directory as its own DLL, next to `KlangHub.exe`, and can be replaced with a
compatible build. Nothing is statically merged into KlangHub. That is what makes the LGPL components
below satisfiable, so it is a property of the build rather than an accident of it.

The full text of every copyleft licence involved is in [`licenses/`](../licenses/) and is installed
alongside the application — LGPL-2.1 §1, LGPL-3.0 §4 and MPL-1.1 §3.5 each ask for a copy of the
licence, and a link is a reference rather than a copy.

---

## Foundational codebase

KlangHub's discovery, casting and streaming core began as a fork of an open-source Chromecast desktop
audio streamer by **SamDel**, released under the MIT License. That licence's required copyright notice
is kept in [LICENSE](../LICENSE). Since then the application has been substantially rewritten and
extended (module boundaries, .NET 10, FLAC, multi-provider discovery, the metadata cascade, the
television stage and more), but the original authorship and terms are gratefully acknowledged.

- **Source:** https://github.com/SamDel/ChromeCast-Desktop-Audio-Streamer

---

## Shipped with the application

### CUETools.Codecs.FLAKE / CUETools.Codecs

- **Purpose:** managed FLAC (FLAKE) live encoder for lossless streaming.
- **Licence:** **GNU Lesser General Public License v3.0 (LGPL-3.0)**.
- **Authors:** Gregory S. Chudov, Justin Ruggles and CUETools contributors (.NET Standard port by
  EntangledBits).
- **Source:** https://github.com/EntangledBits/CUETools.Codecs · NuGet `CUETools.Codecs.FLAKE`
- **Compliance:** shipped unmodified as separate DLLs (`CUETools.Codecs.FLAKE.dll`,
  `CUETools.Codecs.dll`), dynamically loaded, so a user may replace them with a compatible build —
  which is what the LGPL asks of a work that merely uses the library. KlangHub itself stays MIT.
  Full text shipped in [`licenses/LGPL-3.0.txt`](../licenses/LGPL-3.0.txt).

### NAudio

- **Purpose:** WASAPI loopback capture and audio format handling.
- **Licence:** MIT · **Source:** https://github.com/naudio/NAudio

### NAudio.Lame — and LAME itself

- **Purpose:** MP3 encoding for the compressed streaming formats.
- **Licence of the wrapper:** MIT, © 2013-2019 Corey Murtagh ·
  **Source:** https://github.com/Corey-M/NAudio.Lame
- **Licence of the native encoder:** the package ships `libmp3lame.32.dll` and `libmp3lame.64.dll`.
  **LAME is licensed under the GNU Lesser General Public License v2.1 (LGPL-2.1)** — a fact worth
  stating plainly rather than in passing, because it is a second LGPL obligation alongside FLAKE.
- **Compliance:** the encoder is a separate, dynamically loaded, unmodified native DLL and can be
  replaced by a compatible build. Full text shipped in
  [`licenses/LGPL-2.1.txt`](../licenses/LGPL-2.1.txt). Source: https://lame.sourceforge.io/
- **Patents:** the MP3 patent pool expired in 2017 worldwide. No patent licence is required to encode.

### Tmds.MDns

- **Purpose:** mDNS / DNS-SD discovery of devices on the local network.
- **Licence:** MIT · **Source:** https://github.com/tmds/Tmds.MDns

### Google.Protobuf

- **Purpose:** runtime for the Cast and Clementine wire formats.
- **Licence:** BSD-3-Clause, © Google Inc. · **Source:** https://github.com/protocolbuffers/protobuf

### Microsoft.Windows.Compatibility

- **Purpose:** Windows-specific APIs on .NET.
- **Licence:** MIT, © Microsoft Corporation · **Source:** https://github.com/dotnet/runtime

### The .NET runtime itself

- **Purpose:** the build is self-contained, so the runtime is installed with the application rather
  than downloaded — `coreclr.dll`, `System.*.dll`, `WinRT.Runtime.dll` and the rest of the shared
  framework are all in the installation directory.
- **Licence:** MIT, © .NET Foundation and contributors, and redistribution is expressly permitted by
  the .NET Library License. · **Source:** https://github.com/dotnet/runtime
- **Listed here because it is shipped.** It arrives through the publish rather than through a
  `PackageReference`, which is exactly how a redistributed component gets forgotten.

### Ude.NetStandard

- **Purpose:** character-encoding detection. Ships as a dependency of z440.atl.core, which uses it to
  work out how a tag was written before reading it.
- **Licence:** a **tri-licence inherited from the Mozilla Universal Charset Detector** — the library
  may be used under the **Mozilla Public License 1.1**, the **GPL v2 or later**, or the
  **LGPL v2.1 or later**, at the user's option.
- **The option taken here:** MPL-1.1 (equivalently LGPL-2.1). Both are satisfied the same way and
  neither reaches KlangHub's own MIT code.
- **Compliance:** shipped unmodified as a separate, dynamically loaded DLL (`Ude.NetStandard.dll`) and
  replaceable by a compatible build. Full texts in [`licenses/`](../licenses/): `MPL-1.1.txt`,
  `LGPL-2.1.txt`.
- **Source:** https://github.com/yinyue200/ude (C# port) · original:
  https://www-archive.mozilla.org/projects/intl/chardet.html

### z440.atl.core (ATL)

- **Purpose:** reading ID3, Vorbis, MP4 and FLAC tags plus embedded cover art, so the television can
  name the artist, title and album of a track KlangHub only hears as loopback audio.
- **Licence:** MIT · **Source:** https://github.com/Zeugma440/atldotnet
- **Ships as:** `ATL.dll` — the assembly name does not match the package name, which is worth stating
  so the file can be found in an installation.
- **Why not TagLib#:** TagLib# is LGPL. The project already carries LGPL obligations for two codecs and
  deliberately does not add a third for a job an MIT library does just as well.

---

## Protocol definitions

A wire format is a fact about how devices talk, not a creative work — but the files describing one are
written by somebody, and those authors are named here.

### Google Cast wire format (`Source/KlangHub.Platform/ProtocolBuffer/CastChannel.proto`)

- **Origin:** restated from the Cast channel definition published in the Chromium source tree
  (`components/media_router/common/providers/cast/channel/`), © The Chromium Authors, under the
  **BSD-3-Clause** licence. Field numbers, enum values and message names are the protocol itself and
  are reproduced exactly; the file was rewritten in proto3 because the C# runtime does not support
  proto2, and several explanatory comments follow the original wording.
- **Licence text:** https://chromium.googlesource.com/chromium/src/+/main/LICENSE

### Clementine network remote (`Source/KlangHub.Platform/ProtocolBuffer/ClementineRemote.proto`)

- **Origin:** reduced and restated from `ext/libclementine-remote/remotecontrolmessages.proto` of the
  Clementine music player, © 2017 David Sansome and Andreas Muttscheller.
- **Licence:** **Apache-2.0**. The Clementine project publishes this one file under Apache-2.0 rather
  than the GPL covering the rest of the player, stating in the file itself that this is so third-party
  applications may speak the protocol without taking on the GPL.
- **Note:** no Clementine code is used or linked. Only the protocol is spoken, over the network port
  the user switches on in Clementine's own settings.

---

## Loaded by the television receiver at run time

The Cast receiver page (`receiver/index.html`) is served from GitHub Pages and fetches two things from
Google when a television opens it. Neither is redistributed by this project.

### Google Cast Application Framework (CAF) Receiver SDK

- **Loaded from:** `//www.gstatic.com/cast/sdk/libs/caf_receiver/v3/cast_receiver_framework.js`
- **Terms:** using it requires an application registered in the Google Cast SDK Developer Console and
  acceptance of the **Google Cast SDK Additional Developer Terms of Service**. The application id is
  therefore deliberately not in this repository: whoever runs their own receiver registers their own
  and enters it in the settings.
- No subresource integrity hash is pinned, on purpose — Google updates this script under a stable v3
  URL, and a pinned hash would break every receiver the moment they ship a patch.

### Fonts: Fraunces and Inter Tight

- **Loaded from:** Google Fonts (`fonts.googleapis.com`, `fonts.gstatic.com`).
- **Licence:** both are published under the **SIL Open Font License 1.1**, which permits use,
  embedding and redistribution, including commercially. The font files are not redistributed here;
  the receiver fetches them at run time.
- **Source:** https://fonts.google.com/specimen/Fraunces · https://fonts.google.com/specimen/Inter+Tight

### Cover Art Archive images (radio only)

- **Loaded from:** `coverartarchive.org`, by the television, when a web radio track names an artist and
  a title and no cover came from the tags, the folder, Clementine or SMTC.

---

## Queried by the application at run time

### MusicBrainz web service → Cover Art Archive

- **Queried from:** `musicbrainz.org/ws/2/recording` — artist and title of the track playing, nothing
  else. No account, no key. The answer yields a release id, which becomes a
  `coverartarchive.org/release/<id>/front-500` address that is handed to the television as a cover.
- **Terms:** the MusicBrainz web service asks for an identifying User-Agent and at most about one
  request per second. KlangHub sends `KlangHub/<version> ( https://github.com/finflowly/KlangHub )`,
  makes at most one request at a time, waits between them, and steps back for five minutes when the
  service answers 503 or 429. Every answer is remembered — misses included — so a station that repeats
  a track is not a second question.
- **Licence:** the MusicBrainz data used here (artist, title, release id) is in the **public domain**
  (CC0). Cover Art Archive images are hosted by the Internet Archive on behalf of MusicBrainz; they are
  fetched by the television and are **not** redistributed by this project, not cached as files, and not
  bundled in any build.
- **Why this catalogue and not a better-stocked one:** the iTunes Search API and Deezer have higher hit
  rates for current radio material, and both are ruled out on purpose. Apple permits its promotional
  artwork only in aid of the store, with an accompanying badge; Deezer's terms are written for personal
  applications and forbid tying its content to another brand. Last.fm and Spotify need keys, and a key
  cannot live in a public repository. MusicBrainz and the Cover Art Archive are the ones a licence
  audit survives.
- **Source:** https://musicbrainz.org/doc/MusicBrainz_API · https://coverartarchive.org

---

## Development only — not shipped

Used to build and test KlangHub; no part of these is in a distributed build.

| Component | Licence |
|---|---|
| Grpc.Tools (runs `protoc` at build time) | Apache-2.0 |
| xUnit.net v3 (`xunit.v3`, `xunit.runner.visualstudio`) | Apache-2.0 |
| NSubstitute | BSD-3-Clause |
| Inno Setup (installer compiler) | Inno Setup licence — free use, including commercial |

---

## Trademarks and independence

KlangHub is an independent open-source project and is **not affiliated with, endorsed by or certified
by** Google, Apple, Spotify, Samsung or Harman. Product and protocol names appear only descriptively,
to say which devices the application works with:

- **Chromecast**, **Google Cast**, **Google Home** — trademarks of Google LLC.
- **AirPlay** — trademark of Apple Inc. KlangHub *discovers* AirPlay devices from the mDNS
  announcements they broadcast openly, and sends **no** AirPlay audio. Apple's pairing and encryption
  are neither implemented nor circumvented; `AirPlayProvider.CreateSession` throws.
- **Spotify**, **Spotify Connect** — trademarks of Spotify AB. **Not supported by KlangHub.**
- **Snapcast** — a project by Johannes Pohl (GPL-3.0). Devices are discovered on the network; control
  is not implemented, and no Snapcast code is used or linked.
