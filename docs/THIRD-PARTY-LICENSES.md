# Third-Party Licenses

KlangHub ships the following third-party components. Each is distributed as its own assembly (the
framework-dependent build keeps them as separate, replaceable DLLs); none are statically merged into KlangHub.

## Foundational codebase

KlangHub's discovery, casting and streaming core began as a fork of an open-source Chromecast desktop
audio streamer by **SamDel**, released under the MIT License (see [LICENSE](../LICENSE) for the full
text and required copyright notice). Since then the app has been substantially rewritten and extended
(module boundaries, .NET 10, FLAC, multi-provider discovery, the "hi-fi console" UI and more), but the
original authorship and license terms are gratefully acknowledged here.

## CUETools.Codecs.FLAKE / CUETools.Codecs

- **Purpose:** managed FLAC (FLAKE) live-encoder used for lossless audio streaming.
- **License:** GNU Lesser General Public License v3.0 (LGPL-3.0).
- **Authors:** Gregory S. Chudov, Justin Ruggles, and CUETools contributors (.NET Standard port by
  EntangledBits).
- **Source:** https://github.com/EntangledBits/CUETools.Codecs · NuGet `CUETools.Codecs.FLAKE`.
- **Compliance:** shipped unmodified as separate DLLs (`CUETools.Codecs.FLAKE.dll`, `CUETools.Codecs.dll`).
  Users may replace them with a compatible build. Full license text: https://www.gnu.org/licenses/lgpl-3.0.html

## NAudio / NAudio.Lame

- **License:** MIT (NAudio) · LGPL-derived libmp3lame is shipped by NAudio.Lame as a native DLL.
- **Source:** https://github.com/naudio/NAudio · https://github.com/Corey-M/NAudio.Lame

## Tmds.MDns

- **Purpose:** mDNS / DNS-SD discovery.
- **License:** MIT · **Source:** https://github.com/tmds/Tmds.MDns
