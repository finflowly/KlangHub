# Third-Party Licenses

KlangHub ships the following third-party components. Each is distributed as its own assembly (the
framework-dependent build keeps them as separate, replaceable DLLs); none are statically merged into KlangHub.

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
